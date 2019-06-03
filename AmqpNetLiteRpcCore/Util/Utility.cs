namespace AmqpNetLiteRpcCore
{
    public class AmqpRpcNode
    {
        public string Address { get; set; }
        public string Subject { get; set; }
    }


    public class Utility
    {
        public static AmqpRpcNode ParseRpcNodeAddress(string nodeAddress)
        {
            var result = new AmqpRpcNode();
            if (!nodeAddress.Contains("/"))
            {
                result.Address = nodeAddress;
                return result;
            }
            var tempAddress = nodeAddress.Split('/');
            if (tempAddress.Length <= 0)
            {
                throw new AmqpRpcInvalidNodeAddressException($"Invalid address {nodeAddress}");
            }
            result.Address = tempAddress[0];
            result.Subject = tempAddress[1];
            return result;
        }
    }
}