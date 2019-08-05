using Amqp;
using Amqp.Framing;
using AmqpNetLiteRpcCore.Util;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AmqpNetLiteRpcCore
{
    internal class RpcClient : RpcBase, IRpcClient
    {
        private ISession _session = null;
        private ISenderLink _sender = null;
        private IReceiverLink _receiver = null;
        private string _subject = string.Empty;
        private string _amqpNode = string.Empty;
        private string _clientReceiveAddress = string.Empty;

        private uint _timeout = 30000;
        private ManualResetEvent _receiverAttached = new ManualResetEvent(false);
        private int _receiverAttacheTimeout = 10000;

        private bool _isTimedout = false;

        private ConcurrentDictionary<string, IPendingRequest> _pendingRequests = new ConcurrentDictionary<string, IPendingRequest>();

        public uint Timeout
        {
            set
            {
                this._timeout = value;
            }
        }

        public RpcClient(string amqpNodeAddress, ISession session)
        {
            this._amqpNode = amqpNodeAddress;
            this._session = session;
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

        private async Task<T> SendRequestAsync<T>(AmqpRpcRequest request) where T : class
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
                try
                {
                    var tcs = new TaskCompletionSource<T>();
                    this._pendingRequests.TryAdd(id, new PendingRequest<T>(tcs));
                    return await tcs.Task.TimeoutAfterAsync<T>(timeout: unchecked((int)this._timeout));
                }
                catch (TimeoutException)
                {
                    this._isTimedout = true;
                    throw new AmqpRpcRequestTimeoutException($"Request timedout while executing {request.Method}");
                }
            }
            return null;
        }

        private void processResponse(IReceiverLink receiver, Message message)
        {
            receiver.Accept(message);
            if (!this._pendingRequests.TryRemove(message.Properties.CorrelationId, out var tcs))
            {
                Console.WriteLine($"No pending response for {message.Properties.CorrelationId}");
            }
            if (!this._isTimedout)
            {
                var response = message.GetBody<AmqpRpcResponse>();
                if (tcs != null)
                {
                    tcs.SetResult(response);
                }
            }
        }

        public async Task<T> CallAsync<T>(string functionName, object parameter = null) where T : class
        {
            var request = new AmqpRpcRequest()
            {
                Method = functionName,
                Parameters = parameter,
                Type = RpcRequestType.Call
            };

            return await this.SendRequestAsync<T>(request);
        }

        public async Task NotifyAsync(string functionName, object parameter = null)
        {
            var request = new AmqpRpcRequest()
            {
                Method = functionName,
                Parameters = parameter,
                Type = RpcRequestType.Call
            };

            await this.SendRequestAsync<object>(request);
        }

        public void Create(IMessageOptions options = null)
        {
            if (options != null)
            {
                if (options.Timeout > 0)
                {
                    this._timeout = options.Timeout;
                }
            }
            var nodeAddress = this.ParseRpcNodeAddress(this._amqpNode);
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

            this._receiver = this._session.CreateReceiver(name: $"AmqpNetLiteRpcClientReceiver-{this._amqpNode}", source: _receiverSource, onAttached: OnReceiverLinkAttached);
            this._receiver.Start(credit: int.MaxValue, onMessage: this.processResponse);
            this._sender = this._session.CreateSender(name: $"AmqpNetLiteRpcClientSender-{this._amqpNode}", target: new Target(), onAttached: OnSenderLinkAttached);
            if (!_receiverAttached.WaitOne(this._receiverAttacheTimeout))
            {
                throw new Exception($"Failed to create receiver connection in {this._receiverAttacheTimeout}");
            }
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