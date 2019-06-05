using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Amqp.Types;
using Newtonsoft.Json;
using Serilog;

namespace AmqpNetLiteRpcCore
{
    public class RpcClient
    {
        private Connection _connection = null;
        private Session _session = null;
        private SenderLink _sender = null;
        private ReceiverLink _receiver = null;
        private string _subject = string.Empty;
        private string _amqpNode = string.Empty;
        private string _clientReceiveAddress = string.Empty;

        private uint _timeout = 30000;
        private ManualResetEvent _receiverAttached = new ManualResetEvent(false);
        private int _receiverAttacheTimeout = 10000;

        private bool _isTimedout = false;


        public uint Timeout
        {
            set
            {
                this._timeout = value;
            }
        }

        public RpcClient(string amqpNodeAddress, Connection connection)
        {
            this._amqpNode = amqpNodeAddress;
            this._connection = connection;
            this._session = new Session(this._connection);
        }

        private void OnReceiverLinkAttached(ILink _link, Attach attach)
        {
            this._clientReceiveAddress = ((Source)attach.Source).Address;
            Log.Information($"RpcClient receiver is connected to {this._amqpNode} on address {this._clientReceiveAddress}");
            this._receiverAttached.Set();
        }

        private void OnSenderLinkAttached(ILink _link, Attach attach)
        {
            Log.Information($"RpcClient sender is connected to {this._amqpNode}{(!string.IsNullOrEmpty(this._subject) ? string.Format("/{0}", this._subject) : string.Empty)}");
        }

        private async Task<T> SendRequest<T>(AmqpRpcRequest request) where T : class
        {
            var _message = new Message() { BodySection = new AmqpValue<AmqpRpcRequest>(request) };
            var id = Guid.NewGuid().ToString();
            _message.Properties = new Properties()
            {
                Subject = this._subject,
                To = this._amqpNode,
                ReplyTo = request.Type.Equals(RpcRequestType.Call) ? this._clientReceiveAddress : string.Empty,
                MessageId = id,
                CorrelationId = id
            };
            _message.Header = new Header()
            {
                Ttl = this._timeout
            };
            await this._sender.SendAsync(_message);
            if (request.Type.Equals(RpcRequestType.Call))
            {
                var receiverWaitTask = Task.Run<AmqpRpcResponse>(function: this.processResponse);
                try
                {
                    var _response = await receiverWaitTask.TimeoutAfter(timeout: unchecked((int)this._timeout));
                    if (_response.ResponseCode.Equals(RpcResponseType.Ok))
                    {
                        return new Utility().GetRequestMessage(deserializationType: typeof(T), _response.ResponseMessage);
                    }
                    else if (_response.ResponseCode.Equals(RpcResponseType.Error))
                    {
                        var _err = new Utility().GetRequestMessage(deserializationType: typeof(AmqpRpcServerException), _response.ResponseMessage) as AmqpRpcServerException;
                        throw new AmqpRpcException(_err.Message, _err.Stack, _err.Code);
                    }
                }
                catch (TimeoutException)
                {
                    this._isTimedout = true;
                    throw new AmqpRpcRequestTimeoutException($"Request timedout while executing {request.Method}");
                }
            }
            return null;
        }

        private AmqpRpcResponse processResponse()
        {
            var _response = this._receiver.Receive();
            this._receiver.Accept(_response);
            if (this._isTimedout)
            {
                return null;
            }
            else
            {
                return _response.GetBody<AmqpRpcResponse>();
            }
        }

        public async Task<T> Call<T>(string functionName, object parameter = null) where T : class
        {
            var request = new AmqpRpcRequest()
            {
                Method = functionName,
                Parameters = parameter,
                Type = RpcRequestType.Call
            };

            return await this.SendRequest<T>(request);
        }

        public async Task Notify(string functionName, object parameter = null)
        {
            var request = new AmqpRpcRequest()
            {
                Method = functionName,
                Parameters = parameter,
                Type = RpcRequestType.Call
            };

            await this.SendRequest<object>(request);
        }

        public void Connect()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine("logs", "AmqpNetLiteRpcClientLogs.txt"), rollOnFileSizeLimit: true)
                .CreateLogger();
            var nodeAddress = Utility.ParseRpcNodeAddress(this._amqpNode);
            this._amqpNode = nodeAddress.Address;
            if (!string.IsNullOrEmpty(nodeAddress.Subject))
            {
                this._subject = nodeAddress.Subject;
            }

            var _receiverSource = new Source()
            {
                Dynamic = true,
                Address = nodeAddress.Address
            };

            this._receiver = new ReceiverLink(session: this._session, name: "AmqpNetLiteRpcClientReceiver", source: _receiverSource, onAttached: OnReceiverLinkAttached);
            this._sender = new SenderLink(session: this._session, name: "AmqpNetLiteRpcClientSender", target: new Target(), onAttached: OnSenderLinkAttached);
            if (!_receiverAttached.WaitOne(this._receiverAttacheTimeout))
            {
                throw new Exception($"Failed to create receiver connection in {this._receiverAttacheTimeout}");
            }
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