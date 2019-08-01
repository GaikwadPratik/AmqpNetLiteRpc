using System;

namespace AmqpNetLiteRpcCore
{
    public class RpcRequestObjectType
    {
        public Type FunctionWrapperType { get; set; }
        public Type RequestParameterType { get; set; }
    }
}