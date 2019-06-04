using Amqp;
using Amqp.Serialization;

namespace AmqpNetLiteRpcCore
{
    [AmqpContract(Encoding = EncodingType.SimpleMap)]
    public class AmqpRpcRequest
    {
        [AmqpMember(Name = "method")]
        public string Method { get; set; }
        [AmqpMember(Name = "params")]
        public object Parameters { get; set; }
        [AmqpMember(Name = "type")]
        public string Type { get; set; }
    }
}