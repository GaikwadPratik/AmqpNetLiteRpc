using Amqp;
using Amqp.Serialization;
using AmqpNetLiteRpcCore;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace AmqpNetLiteRpcConsole
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Address _address = new Address(host: "192.168.122.2", port: 5672, user: "system", password: "manager", scheme: "amqp");
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
                Console.WriteLine(JsonConvert.SerializeObject(c));
                c = await _rpcClient.CallAsync<TestRequestMap>(functionName: "simpleParams", parameter: new TestRequestList() { firstName = "123", lastName = "456" });
                Console.WriteLine(JsonConvert.SerializeObject(c));
                c = await _rpcClient.CallAsync<TestRequestMap>(functionName: "namedParams", parameter: new TestRequestMap() { firstName = "123", lastName = "456" });
                Console.WriteLine(JsonConvert.SerializeObject(c));
                var D = await _rpcClient.CallAsync<object>(functionName: "nullResponse");
                Console.WriteLine(D is null);
                await _rpcClient.CallAsync<TestRequestMap>(functionName: "namedParams");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            Console.ReadLine();
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
            return new TestRequestMap() { firstName = "123", lastName = "456" };
        }

        [RpcMethod]
        public TestRequestMap simpleParams(TestRequestList request)
        {
            return new TestRequestMap() { firstName = "123", lastName = "456" };
        }

        [RpcMethod]
        public TestRequestMap namedParams(TestRequestMap request)
        {
            return new TestRequestMap() { firstName = "123", lastName = "456" };
        }

        [RpcMethod]
        public void nullResponse()
        {

        }
    }
}
