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
            Address _address = new Address(host: "192.168.122.2", port: 5672, user: "", password: "", scheme: "amqp");
            ConnectionFactory _connFactory = new ConnectionFactory();
            //_connFactory.SSL.ClientCertificates 
            Connection _connection = await _connFactory.CreateAsync(_address);
            RpcServer _rpcServer = new RpcServer(amqpNodeAddress: "amq.topic/test", connection: _connection);
            _rpcServer.Connect();
            _rpcServer.Bind();
            // _rpcServer.Bind(methodName: "noParams", new RpcRequestObjectTypes() { FunctionWrapperType = typeof(Test) });
            // _rpcServer.Bind(methodName: "simpleParams", new RpcRequestObjectTypes() { FunctionWrapperType = typeof(Test), RequestParameterType = typeof(TestRequestList) });
            // _rpcServer.Bind(methodName: "namedParams", new RpcRequestObjectTypes() { FunctionWrapperType = typeof(Test), RequestParameterType = typeof(TestRequestMap) });
            // _rpcServer.Bind(methodName: "nullResponse", new RpcRequestObjectTypes() { FunctionWrapperType = typeof(Test) });

            var _rpcClient = new RpcClient(amqpNodeAddress: "amq.topic/test", connection: _connection);
            _rpcClient.Connect();
            try
            {
                var c = await _rpcClient.Call<TestRequestMap>("noParams");
                Console.WriteLine(JsonConvert.SerializeObject(c));
                c = await _rpcClient.Call<TestRequestMap>("simpleParams", new TestRequestList() { firstName = "123", lastName = "456" });
                Console.WriteLine(JsonConvert.SerializeObject(c));
                c = await _rpcClient.Call<TestRequestMap>("namedParams", new TestRequestMap() { firstName = "123", lastName = "456" });
                Console.WriteLine(JsonConvert.SerializeObject(c));
                var D = await _rpcClient.Call<object>("nullResponse");
                Console.WriteLine(D is null);
                await _rpcClient.Call<TestRequestMap>("namedParams");
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
