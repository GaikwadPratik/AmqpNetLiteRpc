using Amqp;
using Serilog;
using Serilog.Formatting.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace AmqpNetLiteRpcCore
{
    public class AmqpClient
    {
        private IConnection _connection = null;
        private Dictionary<string, IRpcClient> _clientMap = new Dictionary<string, IRpcClient>();
        private Dictionary<string, IRpcServer> _serverMap = new Dictionary<string, IRpcServer>();
        private ISession _session = null;

        public void InitiateAmqpRpc(Connection connection)
        {
            this._connection = connection ?? throw new NullReferenceException("Amqp connection is null");
            if (this._connection.IsClosed)
            {
                throw new AmqpRpcException("Amqp connection is closed");
            }
            this._session = this._connection.CreateSession();
        }

        public IRpcClient CreateAmqpRpcClient(string amqpNode, IMessageOptions options = null)
        {
            Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(formatter: new JsonFormatter(), path: Path.Combine("logs", "AmqpNetLiteRpcClientLogs.txt"), rollOnFileSizeLimit: true)
                    .CreateLogger();
            if (this._connection == null)
            {
                throw new Exception("Please initiate connection using InitiateAmqpRpc");
            }
            if (this._session == null)
            {
                throw new Exception("Please initiate session using InitiateAmqpRpc");
            }
            IRpcClient _client = null;
            if (!this._clientMap.TryGetValue(amqpNode, out _client))
            {
                _client = new RpcClient(amqpNode, this._session);
                _client.Create(options);
                this._clientMap.Add(amqpNode, _client);
            }
            return _client;
        }

        public IRpcServer CreateAmqpRpcServer(string amqpNode)
        {
            Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(formatter: new JsonFormatter(), path: Path.Combine("logs", "AmqpNetLiteRpcServerLogs.txt"), rollOnFileSizeLimit: true)
                    .CreateLogger();
            if (this._connection == null)
            {
                throw new Exception("Please initiate connection using InitiateAmqpRpc");
            }
            if (this._session == null)
            {
                throw new Exception("Please initiate session using InitiateAmqpRpc");
            }
            IRpcServer _server = null;
            if (!this._serverMap.TryGetValue(amqpNode, out _server))
            {
                _server = new RpcServer(amqpNode, this._session);
                _server.Create();
                this._serverMap.Add(amqpNode, _server);
            }
            return _server;
        }

        public async void CloseRpcClientAsync(string amqpNode)
        {
            IRpcClient _client = null;
            if (this._clientMap.TryGetValue(amqpNode, out _client))
            {
                if (_client != null)
                    await _client.DestroyAsync();
            }
            if (this._clientMap.ContainsKey(amqpNode))
            {
                this._clientMap.Remove(amqpNode);
            }
        }

        public async void CloseRpcServerAsync(string amqpNode)
        {
            IRpcServer _server = null;
            if (this._serverMap.TryGetValue(amqpNode, out _server))
            {
                if (_server != null)
                    await _server.DestroyAsync();
            }
            if (this._serverMap.ContainsKey(amqpNode))
            {
                this._serverMap.Remove(amqpNode);
            }
        }
    }
}