using Amqp;
using Amqp.Serialization;
using AmqpNetLiteRpcCore;
using System;
using System.Threading.Tasks;

namespace AmqpNetLiteRpcConsole
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            //string connectionString = "amqp://system:manager@192.168.122.2:5672";
            Address _address = new Address(host: "192.168.122.2", port: 5672, user: "system", password: "manager", scheme: "amqp");
            ConnectionFactory _connFactory = new ConnectionFactory();
            //_connFactory.SSL.ClientCertificates 
            Connection _connection = await _connFactory.CreateAsync(_address);
            RpcServer _rpcServer = new RpcServer(amqpNodeAddress: "amq.topic/test", connection: _connection);
            _rpcServer.Connect();
            _rpcServer.Bind(methodName: "noParams", new RpcRequestObjectTypes() { FunctionWrapperType = typeof(Test) });
            _rpcServer.Bind(methodName: "simpleParams", new RpcRequestObjectTypes() { FunctionWrapperType = typeof(Test), RequestParameterType = typeof(TestRequestList) });
            _rpcServer.Bind(methodName: "namedParams", new RpcRequestObjectTypes() { FunctionWrapperType = typeof(Test), RequestParameterType = typeof(TestRequestMap) });
            //rpcServer.Disconnect();
            Console.ReadLine();
        }
    }

    [AmqpContract(Encoding = EncodingType.SimpleList)]
    public class TestRequestList
    {
        [AmqpMember(Order = 1)]
        public string firstName { get; set; }
        [AmqpMember(Order = 2)]
        public string lastName { get; set; }
    }

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
        public string noParams()
        {
            return $"123 456";
        }

        public string simpleParams(TestRequestList request)
        {
            return $"{request.firstName} {request.lastName}";
        }

        public string namedParams(TestRequestMap request)
        {
            return $"{request.firstName} {request.lastName}";
        }
    }
}
