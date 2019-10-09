using Amqp.Serialization;
using System;

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

    public class AmqpRpcMissingAttributeException : Exception
    {
        public string Code { get; set; } = ErrorCode.AmqpRpcMissingAttributeException;
        public AmqpRpcMissingAttributeException(string message) : base(message)
        { }
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
        public string Code { get; set; } = ErrorCode.AmqpRpcRequestTimeOut;
        public AmqpRpcRequestTimeoutException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcMissingFunctionDefinitionException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code { get; set; } = ErrorCode.AmqpRpcMissingFunctionDefinition;
        public AmqpRpcMissingFunctionDefinitionException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcMissingFunctionNameException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code { get; set; } = ErrorCode.AmqpRpcMissingFunctionName;
        public AmqpRpcMissingFunctionNameException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcDuplicateFunctionDefinitionException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code { get; set; } = ErrorCode.AmqpRpcDuplicateFunctionDefinition;
        public AmqpRpcDuplicateFunctionDefinitionException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcParamsNotObjectException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code { get; set; } = ErrorCode.AmqpRpcParamsNotObject;
        public AmqpRpcParamsNotObjectException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcParamsMissingPropertiesException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code { get; set; } = ErrorCode.AmqpRpcParamsMissingProperties;
        public AmqpRpcParamsMissingPropertiesException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcUnknowParameterException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code { get; set; } = ErrorCode.AmqpRpcUnknownParameter;
        public AmqpRpcUnknowParameterException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcUnknownFunctionException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code { get; set; } = ErrorCode.AmqpRpcUnknownFunction;
        public AmqpRpcUnknownFunctionException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcFunctionDefinitionValidationException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code { get; set; } = ErrorCode.AmqpRpcFunctionDefinitionValidationError;
        public AmqpRpcFunctionDefinitionValidationException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcInvalidNodeAddressException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code { get; set; } = ErrorCode.AmqpRpcInvalidNodeAddressException;
        public AmqpRpcInvalidNodeAddressException(string message) : base(message)
        {
        }
    }

    [AmqpContract]
    public class AmqpRpcInvalidRpcTypeException : Exception
    {
        [AmqpMember(Name = "code")]
        public string Code { get; set; } = ErrorCode.AmqpRpcInvalidRpcTypeException;
        public AmqpRpcInvalidRpcTypeException(string message) : base(message)
        {
        }
    }
}