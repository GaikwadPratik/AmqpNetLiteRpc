using System;
using Amqp.Serialization;

namespace AmqpNetLiteRpcCore
{
    public class AmqpRpcException : Exception
    {
        private string _stackTrace = string.Empty;
        private string _code = string.Empty;
        public string Code
        {
            get
            {
                return this._code;
            }
        }
        public override string StackTrace
        {
            get
            {
                return this._stackTrace;
            }
        }
        public AmqpRpcException(string message = "", string stackTrace = null, string code = null) : base(message)
        {
            this._stackTrace = stackTrace;
            this._code = code;
        }
    }

    [AmqpContract(Encoding = EncodingType.SimpleMap)]
    public class AmqpRpcServerException
    {
        [AmqpMember(Name = "code")]
        public string Code { get; set; }
        [AmqpMember(Name = "message")]
        public string Message { get; set; }
        [AmqpMember(Name = "stack")]
        public string Stack { get; set; }
    }

    [AmqpContract]
    public class AmqpRpcRequestTimeoutException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code = ErrorCode.AmqpRpcRequestTimeOut;
        public AmqpRpcRequestTimeoutException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcMissingFunctionDefinitionException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code = ErrorCode.AmqpRpcMissingFunctionDefinition;
        public AmqpRpcMissingFunctionDefinitionException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcMissingFunctionNameException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code = ErrorCode.AmqpRpcMissingFunctionName;
        public AmqpRpcMissingFunctionNameException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcDuplicateFunctionDefinitionException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code = ErrorCode.AmqpRpcDuplicateFunctionDefinition;
        public AmqpRpcDuplicateFunctionDefinitionException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcParamsNotObjectException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code = ErrorCode.AmqpRpcParamsNotObject;
        public AmqpRpcParamsNotObjectException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcParamsMissingPropertiesException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code = ErrorCode.AmqpRpcParamsMissingProperties;
        public AmqpRpcParamsMissingPropertiesException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcUnknowParameterException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code = ErrorCode.AmqpRpcUnknownParameter;
        public AmqpRpcUnknowParameterException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcUnknownFunctionException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code = ErrorCode.AmqpRpcUnknownFunction;
        public AmqpRpcUnknownFunctionException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcFunctionDefinitionValidationException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code = ErrorCode.AmqpRpcFunctionDefinitionValidationError;
        public AmqpRpcFunctionDefinitionValidationException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcInvalidNodeAddressException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code = ErrorCode.AmqpRpcInvalidNodeAddressException;
        public AmqpRpcInvalidNodeAddressException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcInvalidRpcTypeException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code = ErrorCode.AmqpRpcInvalidRpcTypeException;
        public AmqpRpcInvalidRpcTypeException(string message) : base(message)
        {
        }
    }
}