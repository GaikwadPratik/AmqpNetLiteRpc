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

namespace AmqpeNetLiteRpcCore
{
    [AmqpContract]
    public class TestRequest
    {
        [AmqpMember]
        public string firstName { get; set; }
        [AmqpMember]
        public string lastName { get; set; }
    }
    class RequestObjectTypes
    {
        public Type FunctionWrapperType { get; set; }
        public Type RequestParameterType { get; set; }
    }

    public class RpcServer
    {
        private Connection _connection = null;
        private Session _session = null;
        private SenderLink _sender = null;
        private ReceiverLink _receiver = null;
        private string _subject = string.Empty;
        private string _amqpNode = string.Empty;

        private Dictionary<string, RequestObjectTypes> _serverFunctions = new Dictionary<string, RequestObjectTypes>();

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
                Log.Error("Invalid request type received", _rpcRequest.type);
                await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, _rpcRequest.type, null, new AmqpRpcInvalidRpcTypeException($"{_rpcRequest.type}"));
                return;
            }
            if (string.IsNullOrEmpty(_rpcRequest.method))
            {
                Log.Error("Missing RPC function call name", _rpcRequest);
            }
            if (!this._serverFunctions.ContainsKey(_rpcRequest.method))
            {
                Log.Error("Unknown RPC function call request received", _rpcRequest.method);
                await this.SendResponse(replyTo: _replyTo, correlationId: _correlationId, _rpcRequest.type, null, new AmqpRpcUnknownFunctionException($"{_rpcRequest.method} is not bound to remote server"));
                return;
            }
            var _requestObjectType = this._serverFunctions.SingleOrDefault(x => x.Key.Equals(_rpcRequest.method));
            if (_requestObjectType.Value == null) 
            {
                //TODO: Log error message and continue
            }
            // await Task.Run(() => {
                Console.WriteLine(_rpcRequest);
                var _methodParameter = this.GetRequestMessage(_requestObjectType.Value.RequestParameterType, _rpcRequest.parameters);

                var _classInstance = Activator.CreateInstance(_requestObjectType.Value.FunctionWrapperType);
                MethodInfo _method = _requestObjectType.Value.FunctionWrapperType.GetMethod(_requestObjectType.Key);
                _method.Invoke(_classInstance, new [] {_methodParameter});
            // });
        }

        private dynamic GetRequestMessage(Type deserializationType, object parameters)
        {
            try 
            {
                AmqpSerializer _serializer = new AmqpSerializer();
                ByteBuffer _paramsBuffer = new ByteBuffer(1024, true);
                _serializer.WriteObject(_paramsBuffer, parameters);
                //ByteBuffer b = new ByteBuffer(1024, true);
                //AmqpSerializer.Serialize(b, parameters);
                //var a = AmqpSerializer.Deserialize<TestRequest>(_paramsBuffer);
                //var p = AmqpSerializer.Deserialize<TestRequest>(b);
                //AmqpSerializer s = new AmqpSerializer();
                var c = (TestRequest)_serializer.ReadObject<TestRequest>(_paramsBuffer);
                // var _readObjectMethodInfo = typeof(AmqpSerializer).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                //     .Where(x => x.Name.Equals("ReadObject") && x.IsGenericMethod && x.GetGenericArguments().Length.Equals(1))
                //     .FirstOrDefault();
                // //var _readObjectMethodInfo = typeof(AmqpSerializer).GetMethod("ReadObject");
                // if (_readObjectMethodInfo == null) 
                // {
                //     throw new MissingMethodException("ReadObject from AmqpSerializer");
                // }
                // var _readObjectGenericMethodInfo = _readObjectMethodInfo.MakeGenericMethod(typeof(TestRequest));
                // var deserialized = _readObjectGenericMethodInfo.Invoke(_serializer, new [] {_paramsBuffer});
                // return deserialized;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Log.Error("Exception while deserializing request parameters", ex);
            }
            return null;
        }

        private async Task SendResponse(string replyTo, string correlationId, string requestType, object response, Exception ex)
        {

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

        public void Bind(string methodName, Type functionWrapperType, Type requestParameterType)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new AmqpRpcMissingFunctionNameException("Function name is missing during definition binding");
            }
            if (this._serverFunctions.ContainsKey(methodName))
            {
                throw new AmqpRpcDuplicateFunctionDefinitionException($"{methodName} is already bound to RPC server");
            }
            var _requestObjectType = new RequestObjectTypes()
            {
                FunctionWrapperType = functionWrapperType,
                RequestParameterType = requestParameterType
            };
            this._serverFunctions.Add(methodName, _requestObjectType);
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
