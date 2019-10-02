using Amqp;
using Amqp.Framing;
using Amqp.Serialization;
using Amqp.Types;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AmqpNetLiteRpcCore
{
    internal class RpcServer : RpcBase, IRpcServer
    {
        private ISession _session = null;
        private ISenderLink _sender = null;
        private IReceiverLink _receiver = null;
        private string _subject = string.Empty;
        private string _amqpNode = string.Empty;

        private Dictionary<string, RpcRequestObjectType> _serverFunctions = new Dictionary<string, RpcRequestObjectType>();
        private readonly Utility _utility = new Utility();
        private readonly List<string> _rpMethodTypes = typeof(RpcRequestType)
                    .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    .Where(x => x.IsLiteral && !x.IsInitOnly)
                    .Select(x => x.GetRawConstantValue() as string)
                    .ToList();

        public RpcServer(string amqpNodeAddress, ISession session)
        {
            this._amqpNode = amqpNodeAddress;
            this._session = session;
        }

        private async void ProcessIncomingRpcRequestAsync(IReceiverLink receiver, Message message)
        {
            //Accept the message since we would be replying using sender, hence disposition does not make sense
            await Task.Run(async () =>
            {
                receiver.Accept(message);
                //Deserialize the body
                AmqpRpcRequest _rpcRequest = message.GetBody<AmqpRpcRequest>();
                string _replyTo = message.Properties.ReplyTo;
                string _correlationId = message.Properties.CorrelationId;

                if (!_rpMethodTypes.Contains(_rpcRequest.Type))
                {
                    Log.Error($"Invalid request type received: {_rpcRequest.Type}");
                    await this.SendResponseAsync(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: null, ex: new AmqpRpcInvalidRpcTypeException(_rpcRequest.Type));
                    return;
                }
                if (string.IsNullOrEmpty(_rpcRequest.Method))
                {
                    Log.Error("Missing RPC function call name: {@_rpcRequest}", _rpcRequest);
                    await this.SendResponseAsync(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: null, ex: new AmqpRpcMissingFunctionNameException(JsonConvert.SerializeObject(_rpcRequest)));
                    return;
                }
                if (!this._serverFunctions.ContainsKey(_rpcRequest.Method))
                {
                    Log.Error($"Unknown RPC method request received: {_rpcRequest.Method}");
                    await this.SendResponseAsync(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: null, ex: new AmqpRpcUnknownFunctionException($"{_rpcRequest.Method} is not bound to remote server"));
                    return;
                }
                var _requestObjectType = this._serverFunctions.SingleOrDefault(x => x.Key.Equals(_rpcRequest.Method));
                if (_requestObjectType.Value == null)
                {
                    Log.Error($"Unknown RPC method request received: {_rpcRequest.Method}");
                    await this.SendResponseAsync(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: null, ex: new AmqpRpcUnknownFunctionException($"{_rpcRequest.Method} is not bound to remote server"));
                    return;
                }

                var _methodParameter = this._utility.PeeloutAmqpWrapper(deserializationType: _requestObjectType.Value.RequestParameterType, parameters: _rpcRequest.Parameters);
                var _classInstance = Activator.CreateInstance(_requestObjectType.Value.FunctionWrapperType);
                MethodInfo _method = _requestObjectType.Value.FunctionWrapperType.GetMethod(_requestObjectType.Key);

                var _asyncAttribute = _method.GetCustomAttribute<AsyncStateMachineAttribute>();
                var _isAsync = (_asyncAttribute != null) || (_method.ReturnType.BaseType.Equals(typeof(Task)));

                try
                {
                    if (!(_methodParameter is null) && _method.GetParameters().Length > 0)
                    {
                        //TODO: check for missing properties from rpc calls
                        //await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, _rpcRequest.type, null, new AmqpRpcUnknowParameterException($"{_rpcRequest.method} invokation failed, mismatch in parameter"));
                        object _rtnVal = null;
                        if (_isAsync)
                        {
                            _rtnVal = await (dynamic)_method.Invoke(_classInstance, new[] { _methodParameter });
                        }
                        else
                        {
                            _rtnVal = _method.Invoke(_classInstance, new[] { _methodParameter });
                        }
                        await this.SendResponseAsync(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: _rtnVal, ex: null);
                    }
                    else if (_methodParameter is null && _method.GetParameters().Length.Equals(0))
                    {
                        object _rtnVal = null;
                        if (_isAsync)
                        {
                            _rtnVal = await (dynamic)_method.Invoke(_classInstance, null);
                        }
                        else
                        {
                            _rtnVal = _method.Invoke(_classInstance, null);
                        }
                        await this.SendResponseAsync(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: _rtnVal, ex: null);
                    }
                    else
                    {
                        await this.SendResponseAsync(replyTo: _replyTo, correlationId: _correlationId, requestType: _rpcRequest.Type, response: null, ex: new AmqpRpcUnknowParameterException($"{_rpcRequest.Method} invocation failed, mismatch in parameter"));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(JsonConvert.SerializeObject(ex), $"Error in sending response replyTo: {_replyTo}");
                }

                return;
            });
        }

        private async Task SendResponseAsync(string replyTo, string correlationId, string requestType, object response, Exception ex)
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
            // this._sender = this._session.CreateSender(name: "AmqpNetLiteRpcServerSender", address: replyTo);
            _message.Properties = new Properties()
            {
                CorrelationId = correlationId,
                Subject = this._subject,
                To = replyTo
            };
            _message.Header = new Header()
            {
                Ttl = 10000
            };
            await this._sender.SendAsync(message: _message);
            //await this._sender.DetachAsync(error: null);
        }

        private void OnReceiverLinkAttached(ILink _link, Attach attach)
        {
            Log.Information($"RpcServer receiver is connected to {this._amqpNode}");
        }

        private void OnSenderLinkAttached(ILink _link, Attach attach)
        {
            Log.Information($"RpcServer sender is connected to {this._amqpNode}");
        }

        private void OnReceiverLinkClosed(IAmqpObject sender, Error error)
        {
            Log.Information($"RpcServer receiver is disconnected from {this._amqpNode}");
            if (error != null)
            {
                Log.Error($"Error received during receiver closing event. description: {error.Description}");
            }
        }

        private void VerifyAttributeExistance(ParameterInfo param)
        {
            if (param == null)
            {
                return;
            }
            if (param.ParameterType.GetCustomAttribute(typeof(RpcMethodParameterAttribute)) == null)
            {
                throw new AmqpRpcMissingAttributeException($"Attribute 'RpcMethodParameter' is missing on parameter {param.ParameterType.Name}");
            }
            if (param.ParameterType.GetCustomAttribute(typeof(AmqpContractAttribute)) == null)
            {
                throw new AmqpRpcMissingAttributeException($"Attribute 'AmqpContract' is missing on parameter {param.ParameterType.Name}");
            }
            var _paramField = param.ParameterType.GetProperties().FirstOrDefault(x => x.GetCustomAttribute(typeof(AmqpMemberAttribute)) == null);
            if (_paramField != null)
            {
                throw new AmqpRpcMissingAttributeException($"Attribute 'AmqpMember' is missing on field {_paramField.PropertyType.Name} in {param.ParameterType.Name}");
            }
        }

        public void Bind()
        {
            var _methods = Assembly.GetCallingAssembly()
                                        .GetTypes()
                                        .SelectMany(_type => _type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                        .Where(_method => _method.GetCustomAttributes().OfType<RpcMethodAttribute>().Any())
                                        .ToList();

            foreach (var _method in _methods)
            {
                var _params = _method.GetParameters();
                if (_params.Length > 1)
                {
                    throw new NotSupportedException($"RPC function with more than one parameter is not supported: {_method.Name}");
                }
                var _param = _params.FirstOrDefault();
                var _requestObjectTypes = new RpcRequestObjectType()
                {
                    FunctionWrapperType = _method.DeclaringType,
                    RequestParameterType = _param != null ? _param.ParameterType : null
                };
                this.Bind(_method.Name, _requestObjectTypes);
            }
        }

        public void Bind(string functionName, RpcRequestObjectType requestObjectTypes)
        {
            if (string.IsNullOrEmpty(functionName))
            {
                throw new AmqpRpcMissingFunctionNameException("Function name is missing during definition binding");
            }
            if (this._serverFunctions.ContainsKey(functionName))
            {
                throw new AmqpRpcDuplicateFunctionDefinitionException($"{functionName} is already bound to RPC server");
            }
            if (!requestObjectTypes.FunctionWrapperType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Any(x => x.Name.Equals(functionName)))
            {
                throw new AmqpRpcUnknownFunctionException($"{functionName} is not found in {requestObjectTypes.FunctionWrapperType.Name}");
            }
            var _methodInfo = requestObjectTypes.FunctionWrapperType.GetMethod(functionName);
            var _params = _methodInfo.GetParameters();
            if (_params.Length > 1)
            {
                throw new NotSupportedException($"RPC function with more than one parameter is not supported: {functionName}");
            }
            this.VerifyAttributeExistance(_params.FirstOrDefault());
            this._serverFunctions.Add(functionName, requestObjectTypes);
        }

        public void Create()
        {
            var nodeAddress = this.ParseRpcNodeAddress(this._amqpNode);
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
            this._receiver = this._session.CreateReceiver(name: $"AmqpNetLiteRpcServerReceiver-{this._amqpNode}", source: _source, onAttached: OnReceiverLinkAttached);
            this._sender = this._session.CreateSender(name: $"AmqpNetLiteRpcServerSender-{this._amqpNode}", target: new Target(), onAttached: this.OnSenderLinkAttached);
            this._receiver.Closed += OnReceiverLinkClosed;
            this._receiver.Start(credit: int.MaxValue, onMessage: ProcessIncomingRpcRequestAsync);
        }

        public async Task DestroyAsync()
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
