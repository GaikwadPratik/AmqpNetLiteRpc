using System;
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

                if (!_rpMethodTypes.Contains(_rpcRequest.type))
                {
                    Log.Error($"Invalid request type received: {_rpcRequest.type}");
                    await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.type, response: null, ex: new AmqpRpcInvalidRpcTypeException($"{_rpcRequest.type}"));
                    return;
                }
                if (string.IsNullOrEmpty(_rpcRequest.method))
                {
                    Log.Error("Missing RPC function call name", _rpcRequest);
                    await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.type, response: null, ex: new AmqpRpcMissingFunctionNameException($"{_rpcRequest}"));
                    return;
                }
                if (!this._serverFunctions.ContainsKey(_rpcRequest.method))
                {
                    Log.Error($"Unknown RPC method request received: {_rpcRequest.method}");
                    await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.type, response: null, ex: new AmqpRpcUnknownFunctionException($"{_rpcRequest.method} is not bound to remote server"));
                    return;
                }
                var _requestObjectType = this._serverFunctions.SingleOrDefault(x => x.Key.Equals(_rpcRequest.method));
                if (_requestObjectType.Value == null)
                {
                    Log.Error($"Unknown RPC method request received: {_rpcRequest.method}");
                    await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.type, response: null, ex: new AmqpRpcUnknownFunctionException($"{_rpcRequest.method} is not bound to remote server"));
                    return;
                }

                var _methodParameter = this.GetRequestMessage(deserializationType: _requestObjectType.Value.RequestParameterType, parameters: _rpcRequest.parameters);
                var _classInstance = Activator.CreateInstance(_requestObjectType.Value.FunctionWrapperType);
                MethodInfo _method = _requestObjectType.Value.FunctionWrapperType.GetMethod(_requestObjectType.Key);
                try
                {
                    if (!(_methodParameter is null) && _method.GetParameters().Length > 0)
                    {
                        //TODO: check for missing properties from rpc calls
                        //await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, _rpcRequest.type, null, new AmqpRpcUnknowParameterException($"{_rpcRequest.method} invokation failed, mismatch in parameter"));
                        object _rtnVal = _method.Invoke(_classInstance, new[] { _methodParameter });
                        await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.type, response: _rtnVal, ex: null);
                    }
                    else if (_methodParameter is null && _method.GetParameters().Length.Equals(0))
                    {
                        object _rtnVal = _method.Invoke(_classInstance, null);
                        await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.type, response: _rtnVal, ex: null);
                    }
                    else
                    {
                        await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.type, response: null, ex: new AmqpRpcUnknowParameterException($"{_rpcRequest.method} invokation failed, mismatch in parameter"));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(JsonConvert.SerializeObject(ex), "Error in sending response");
                    //await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, _rpcRequest.type, null, ex);
                }

                return;
            });
        }

        /// <summary>
        /// Converts parameter object from Amqp request to a particular type to be used as input parameter to RPC method being invoked
        /// </summary>
        /// <param name="deserializationType">Type into which object parameters must be converted</param>
        /// <param name="parameters">Input received at AmqpRequest.body.params</param>
        /// <returns>Deserialized object which will be used as an input to Rpc Method</returns>
        private dynamic GetRequestMessage(Type deserializationType, object parameters)
        {
            try
            {
                //Create Amqp serializer instance
                AmqpSerializer _serializer = new AmqpSerializer();
                //Create dynamic buffer
                ByteBuffer _paramsBuffer = new ByteBuffer(1024, true);
                //Write object to buffer
                _serializer.WriteObject(_paramsBuffer, parameters);
                //Get ReadObject methodinfo using reflection 
                var _readObjectMethodInfo = typeof(AmqpSerializer).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.Name.Equals("ReadObject") && x.IsGenericMethod && x.GetGenericArguments().Length.Equals(1))
                    .FirstOrDefault();
                if (_readObjectMethodInfo == null)
                {
                    throw new MissingMethodException("ReadObject from AmqpSerializer");
                }
                //Mark methodinfo as generic
                var _readObjectGenericMethodInfo = _readObjectMethodInfo.MakeGenericMethod(deserializationType);
                //Invoke ReadObject to deserialize object
                return _readObjectGenericMethodInfo.Invoke(_serializer, new[] { _paramsBuffer });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception while deserializing request parameters", parameters);
            }
            return null;
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
                _response.ResponseMessage = ex;
            }
            else
            {
                _response.ResponseCode = RpcResponseType.Ok;
                _response.ResponseMessage = response;
            }

            Message _message = new Message() { BodySection = new AmqpValue<AmqpRpcResponse>(_response) };
            this._sender = new SenderLink(session: this._session, name: "AmqpNetLiteRpcServerResponse", address: replyTo);
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
            this._receiver = new ReceiverLink(session: this._session, name: "AmqpNetLiteRpcServer", attach: _attach, onAttached: OnReceiverLinkAttached);
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
