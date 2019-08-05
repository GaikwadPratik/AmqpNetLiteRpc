using Amqp.Serialization;

namespace AmqpNetLiteRpcCore
{
    [AmqpContract(Encoding = EncodingType.SimpleMap)]
    public class AmqpRpcResponse
    {
        [AmqpMember(Name = "responseCode")]
        public string ResponseCode { get; set; }

        [AmqpMember(Name = "responseMessage")]
        public object ResponseMessage { get; set; }
    }
}