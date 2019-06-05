using System;

namespace AmqpNetLiteRpcCore
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class RpcMethodAttribute : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RpcMethodParameterAttribute : Attribute
    {

    }
}