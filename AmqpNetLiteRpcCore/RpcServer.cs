﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Amqp;
using Amqp.Framing;
using Amqp.Serialization;
using Amqp.Types;
using Serilog;
using System.Text;
using Newtonsoft.Json;

namespace AmqpNetLiteRpcCore
{
    public class RpcServer
    {
        private Connection _connection = null;
        private Session _session = null;
        private SenderLink _sender = null;
        private ReceiverLink _receiver = null;
        private string _subject = string.Empty;
        private string _amqpNode = string.Empty;

        private Dictionary<string, RpcRequestObjectTypes> _serverFunctions = new Dictionary<string, RpcRequestObjectTypes>();

        public RpcServer(string amqpNodeAddress, Connection connection)
        {
            this._amqpNode = amqpNodeAddress;
            this._connection = connection;
            this._session = new Session(this._connection);
        }

        private async void ProcessIncomingRpcRequest(IReceiverLink receiver, Message message)
        {
            //Accept the message since we would be replying using sender, hence disposition does not make sense
            receiver.Accept(message);
            await Task.Run(async () =>
            {
                //Deserialize the body
                AmqpRpcRequest _rpcRequest = message.GetBody<AmqpRpcRequest>();
                string _replyTo = message.Properties.ReplyTo;
                string _correlationId = message.Properties.CorrelationId;
                var _rpMethodTypes = typeof(RpcRequestType)
                    .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    .Where(x => x.IsLiteral && !x.IsInitOnly)
                    .Select(x => x.GetRawConstantValue() as string)
                    .ToList();

                if (!_rpMethodTypes.Contains(_rpcRequest.Type))
                {
                    Log.Error($"Invalid request type received: {_rpcRequest.Type}");
                    await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: null, ex: new AmqpRpcInvalidRpcTypeException(_rpcRequest.Type));
                    return;
                }
                if (string.IsNullOrEmpty(_rpcRequest.Method))
                {
                    Log.Error("Missing RPC function call name: {@_rpcRequest}", _rpcRequest);
                    await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: null, ex: new AmqpRpcMissingFunctionNameException(JsonConvert.SerializeObject(_rpcRequest)));
                    return;
                }
                if (!this._serverFunctions.ContainsKey(_rpcRequest.Method))
                {
                    Log.Error($"Unknown RPC method request received: {_rpcRequest.Method}");
                    await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: null, ex: new AmqpRpcUnknownFunctionException($"{_rpcRequest.Method} is not bound to remote server"));
                    return;
                }
                var _requestObjectType = this._serverFunctions.SingleOrDefault(x => x.Key.Equals(_rpcRequest.Method));
                if (_requestObjectType.Value == null)
                {
                    Log.Error($"Unknown RPC method request received: {_rpcRequest.Method}");
                    await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: null, ex: new AmqpRpcUnknownFunctionException($"{_rpcRequest.Method} is not bound to remote server"));
                    return;
                }

                var _methodParameter = new Utility().GetRequestMessage(deserializationType: _requestObjectType.Value.RequestParameterType, parameters: _rpcRequest.Parameters);
                var _classInstance = Activator.CreateInstance(_requestObjectType.Value.FunctionWrapperType);
                MethodInfo _method = _requestObjectType.Value.FunctionWrapperType.GetMethod(_requestObjectType.Key);
                try
                {
                    if (!(_methodParameter is null) && _method.GetParameters().Length > 0)
                    {
                        //TODO: check for missing properties from rpc calls
                        //await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, _rpcRequest.type, null, new AmqpRpcUnknowParameterException($"{_rpcRequest.method} invokation failed, mismatch in parameter"));
                        object _rtnVal = _method.Invoke(_classInstance, new[] { _methodParameter });
                        await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: _rtnVal, ex: null);
                    }
                    else if (_methodParameter is null && _method.GetParameters().Length.Equals(0))
                    {
                        object _rtnVal = _method.Invoke(_classInstance, null);
                        await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: _rtnVal, ex: null);
                    }
                    else
                    {
                        await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: null, ex: new AmqpRpcUnknowParameterException($"{_rpcRequest.Method} invocation failed, mismatch in parameter"));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(JsonConvert.SerializeObject(ex), $"Error in sending response replyTo: {_replyTo}");
                }

                return;
            });
        }

        private async Task SendResponse(string replyTo, string correlationId, string requestType, object response, Exception ex)
        {
            if (requestType.Equals(RpcRequestType.Notify))
            {
                return;
            }

            var _response = new AmqpRpcResponse();
            if (ex != null)
            {
                _response.ResponseCode = RpcResponseType.Error;
                var _error = new AmqpRpcServerException()
                {
                    Code = ErrorCode.AmqpRpcUnknownParameter,
                    Message = ex.Message,
                    Stack = ex.StackTrace
                };
                _response.ResponseMessage = _error;
            }
            else
            {
                _response.ResponseCode = RpcResponseType.Ok;
                _response.ResponseMessage = response;
            }

            Message _message = new Message() { BodySection = new AmqpValue<AmqpRpcResponse>(_response) };
            this._sender = new SenderLink(session: this._session, name: "AmqpNetLiteRpcServerSender", address: replyTo);
            _message.Properties = new Properties()
            {
                CorrelationId = correlationId,
                Subject = this._subject,
            };
            _message.Header = new Header()
            {
                Ttl = 10000
            };
            await this._sender.SendAsync(message: _message);
            await this._sender.CloseAsync();
        }

        private void OnReceiverLinkAttached(ILink _link, Attach attach)
        {
            Log.Information($"RpcServer receiver is connected to {this._amqpNode}");
        }

        private void OnReceiverLinkClosed(IAmqpObject sender, Error error)
        {
            Log.Information($"RpcServer receiver is disconnected from {this._amqpNode}");
            if (error != null)
            {
                Log.Error($"Error received during receiver closing event. description: {error.Description}");
            }
        }

        public void Bind(string methodName, RpcRequestObjectTypes requestObjectTypes)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new AmqpRpcMissingFunctionNameException("Function name is missing during definition binding");
            }
            if (!requestObjectTypes.FunctionWrapperType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Any(x => x.Name.Equals(methodName)))
            {
                throw new AmqpRpcUnknownFunctionException($"{methodName} is not found in {requestObjectTypes.FunctionWrapperType.Name}");
            }
            if (this._serverFunctions.ContainsKey(methodName))
            {
                throw new AmqpRpcDuplicateFunctionDefinitionException($"{methodName} is already bound to RPC server");
            }
            this._serverFunctions.Add(methodName, requestObjectTypes);
        }

        public void Connect()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine("logs", "AmqpNetLiteRpcServerLogs.txt"), rollOnFileSizeLimit: true)
                .CreateLogger();
            var nodeAddress = Utility.ParseRpcNodeAddress(this._amqpNode);
            Attach _attach = new Attach();
            _attach.Target = new Target();
            Source _source = new Source()
            {
                Address = nodeAddress.Address
            };
            if (!string.IsNullOrEmpty(nodeAddress.Subject))
            {
                this._subject = nodeAddress.Subject;
                _source.FilterSet = new Map();
                _source.FilterSet[new Symbol("topic-filter")] = new DescribedValue(
                    new Symbol("apache.org:legacy-amqp-topic-binding:string"),
                    this._subject);
            }
            _attach.Source = _source;
            this._receiver = new ReceiverLink(session: this._session, name: "AmqpNetLiteRpcServerReceiver", attach: _attach, onAttached: OnReceiverLinkAttached);
            this._receiver.Closed += OnReceiverLinkClosed;
            this._receiver.Start(credit: 100, onMessage: ProcessIncomingRpcRequest);
        }

        public async void Disconnect()
        {
            if (this._receiver != null && !this._receiver.IsClosed)
            {
                await this._receiver.CloseAsync();
            }
            if (this._sender != null && !this._sender.IsClosed)
            {
                await this._sender.CloseAsync();
            }
            if (this._session != null && !this._session.IsClosed)
            {
                await this._session.CloseAsync();
            }
        }
    }
}