using Amqp;
using Amqp.Serialization;
using AmqpNetLiteRpcCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmqpNetLiteRpcConsole
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Address _address = new Address(host: "192.168.122.2", port: 5672, user: "", password: "", scheme: "amqp");
            ConnectionFactory _connFactory = new ConnectionFactory();
            //_connFactory.SSL.ClientCertificates 
            Connection _connection = await _connFactory.CreateAsync(_address);
            var _amqpClient = new AmqpClient();
            _amqpClient.InitiateAmqpRpc(connection: _connection);
            IRpcServer _rpcServer = _amqpClient.CreateAmqpRpcServer(amqpNode: "amq.topic/test");
            _rpcServer.Bind();
            // _rpcServer.Bind(functionName: "noParams", new RpcRequestObjectType() { FunctionWrapperType = typeof(Test) });
            // _rpcServer.Bind(functionName: "simpleParams", new RpcRequestObjectType() { FunctionWrapperType = typeof(Test), RequestParameterType = typeof(TestRequestList) });
            // _rpcServer.Bind(functionName: "namedParams", new RpcRequestObjectType() { FunctionWrapperType = typeof(Test), RequestParameterType = typeof(TestRequestMap) });
            // _rpcServer.Bind(functionName: "nullResponse", new RpcRequestObjectType() { FunctionWrapperType = typeof(Test) });

            IRpcClient _rpcClient = _amqpClient.CreateAmqpRpcClient(amqpNode: "amq.topic/test");
            try
            {
                var c = await _rpcClient.CallAsync<TestRequestMap>(functionName: "noParams");
                Console.WriteLine($"noParams: {JsonConvert.SerializeObject(c)}");
                c = await _rpcClient.CallAsync<TestRequestMap>(functionName: "noParamsAsync");
                Console.WriteLine($"noParamsAsync: {JsonConvert.SerializeObject(c)}");
                c = await _rpcClient.CallAsync<TestRequestMap>(functionName: "simpleParams", parameter: new TestRequestList() { firstName = "123", lastName = "456" });
                Console.WriteLine($"simpleParams: {JsonConvert.SerializeObject(c)}");
                c = await _rpcClient.CallAsync<TestRequestMap>(functionName: "namedParams", parameter: new TestRequestMap() { firstName = "123", lastName = "456" });
                Console.WriteLine($"namedParams: {JsonConvert.SerializeObject(c)}");
                c = await _rpcClient.CallAsync<TestRequestMap>(functionName: "namedParamsAsync", parameter: new TestRequestMap() { firstName = "123", lastName = "456" });
                Console.WriteLine($"namedParamsAsync: {JsonConvert.SerializeObject(c)}");
                var D = await _rpcClient.CallAsync<object>(functionName: "nullResponse");
                Console.WriteLine($"nullresponse: {D is null}");
                for (int i = 0; i < 5; i++)
                {
                    Console.WriteLine(i);
                    List<Task> _lst = new List<Task>();
                    _lst.Add(Task.Run(async () =>
                    {
                        var c1 = await _rpcClient.CallAsync<TestRequestMap>(functionName: "noParams");
                        Console.WriteLine($"noParams: {JsonConvert.SerializeObject(c1)}");
                        Console.WriteLine($"noparams: {c1.firstName.Equals("noParams")}, {c1.lastName.Equals("noParams1")}");
                    }));
                    _lst.Add(Task.Run(async () =>
                    {
                        var c2 = await _rpcClient.CallAsync<TestRequestMap>(functionName: "simpleParams", parameter: new TestRequestList() { firstName = "123", lastName = "456" });
                        Console.WriteLine($"simpleParams: {JsonConvert.SerializeObject(c2)}");
                        Console.WriteLine($"simpleParams: {c2.firstName.Equals("simpleParams")}, {c2.lastName.Equals("simpleParams1")}");
                    }));
                    _lst.Add(Task.Run(async () =>
                    {
                        var c3 = await _rpcClient.CallAsync<TestRequestMap>(functionName: "namedParams", parameter: new TestRequestMap() { firstName = "123", lastName = "456" });
                        Console.WriteLine($"namedParams: {JsonConvert.SerializeObject(c3)}");
                        Console.WriteLine($"namedParams: {c3.firstName.Equals("namedParams")}, {c3.lastName.Equals("namedParams1")}");
                    }));
                    _lst.Add(Task.Run(async () =>
                    {
                        var c4 = await _rpcClient.CallAsync<TestRequestMap>(functionName: "noParamsAsync", parameter: new TestRequestMap() { firstName = "123", lastName = "456" });
                        Console.WriteLine($"noParamsAsync: {JsonConvert.SerializeObject(c4)}");
                        Console.WriteLine($"noParamsAsync: {c4.firstName.Equals("noParams")}, {c4.lastName.Equals("noParams1")}");
                    }));
                    _lst.Add(Task.Run(async () =>
                    {
                        var c5 = await _rpcClient.CallAsync<TestRequestMap>(functionName: "namedParamsAsync", parameter: new TestRequestMap() { firstName = "123", lastName = "456" });
                        Console.WriteLine($"namedParamsAsync: {JsonConvert.SerializeObject(c5)}");
                        Console.WriteLine($"namedParamsAsync: {c5.firstName.Equals("namedParams")}, {c5.lastName.Equals("namedParams1")}");
                    }));
                    _lst.Add(Task.Run(async () =>
                    {
                        var D1 = await _rpcClient.CallAsync<object>(functionName: "nullResponse");
                        Console.WriteLine($"nullresponse: {D1 is null}");
                    }));
                    while (_lst.Count > 0)
                    {
                        var _completedTask = await Task.WhenAny(_lst);
                        _lst.Remove(_completedTask);
                    }
                }
                await _rpcClient.CallAsync<TestRequestMap>(functionName: "namedParams");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            //Console.ReadLine();
        }
    }

    [RpcMethodParameter]
    [AmqpContract(Encoding = EncodingType.SimpleList)]
    public class TestRequestList
    {
        [AmqpMember]
        public string firstName { get; set; }
        [AmqpMember]
        public string lastName { get; set; }
    }

    [RpcMethodParameter]
    [AmqpContract(Encoding = EncodingType.SimpleMap)]
    public class TestRequestMap
    {
        [AmqpMember]
        public string firstName { get; set; }
        [AmqpMember]
        public string lastName { get; set; }
    }

    class Test
    {
        [RpcMethod]
        public TestRequestMap noParams()
        {
            return new TestRequestMap() { firstName = "noParams", lastName = "noParams1" };
        }

        [RpcMethod]
        public TestRequestMap simpleParams(TestRequestList request)
        {
            return new TestRequestMap() { firstName = "simpleParams", lastName = "simpleParams1" };
        }

        [RpcMethod]
        public TestRequestMap namedParams(TestRequestMap request)
        {
            return new TestRequestMap() { firstName = "namedParams", lastName = "namedParams1" };
        }

        [RpcMethod]
        public void nullResponse()
        {

        }

        [RpcMethod]
        public async Task<TestRequestMap> noParamsAsync()
        {
            return await Task.Run(() => new TestRequestMap() { firstName = "noParams", lastName = "noParams1" });
        }

        [RpcMethod]
        public Task<TestRequestMap> namedParamsAsync(TestRequestMap request)
        {
            return Task.Run(() => new TestRequestMap() { firstName = "namedParams", lastName = "namedParams1" });
        }
    }
}
