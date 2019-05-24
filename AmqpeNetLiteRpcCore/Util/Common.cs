using Amqp;
using Amqp.Serialization;

namespace AmqpeNetLiteRpcCore
{
    [AmqpContract(Encoding = EncodingType.SimpleMap)]
    public class AmqpRpcRequest 
    {
        [AmqpMember]
        public string method {get; set;}
        [AmqpMember(Name="params")]
        public object parameters { get; set; }
        [AmqpMember]
        public string type { get; set; }
    }
}