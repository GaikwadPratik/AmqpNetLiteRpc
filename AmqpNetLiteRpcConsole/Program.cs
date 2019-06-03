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
            _rpcServer.Bind(methodName: "namedParams", functionWrapperType: typeof(Test), requestParameterType: typeof(TestRequest));
            //rpcServer.Disconnect();
            Console.ReadLine();
        }
    }

    [AmqpContract(Encoding = EncodingType.SimpleMap)]
    public class TestRequest
    {
        [AmqpMember]
        public double firstName { get; set; }
        [AmqpMember]
        public string lastName { get; set; }
    }

    class Test
    {
        public void namedParams()
        {

        }
    }
}
