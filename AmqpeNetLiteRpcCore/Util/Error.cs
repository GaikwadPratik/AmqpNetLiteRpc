using System;
public class AmqpRpcResponseException : Exception
{
    public string code { get; set; }
    public AmqpRpcResponseException(string message = ""): base(message)
    {
    }
}

public class AmqpRpcRequestTimeoutException : Exception
{    
    public string code = ErrorCode.AmqpRpcRequestTimeOut;
    public AmqpRpcRequestTimeoutException(string message): base(message)
    {
    }
}

public class AmqpRpcMissingFunctionDefinitionException : Exception
{
    public string code = ErrorCode.AmqpRpcMissingFunctionDefinition;
    public AmqpRpcMissingFunctionDefinitionException(string message): base(message)
    {
    }
}

public class AmqpRpcMissingFunctionNameException : Exception
{
    public string code = ErrorCode.AmqpRpcMissingFunctionName;
    public AmqpRpcMissingFunctionNameException(string message): base(message)
    {
    }
}

public class AmqpRpcDuplicateFunctionDefinitionException : Exception
{
    public string code = ErrorCode.AmqpRpcDuplicateFunctionDefinition;
    public AmqpRpcDuplicateFunctionDefinitionException(string message): base(message)
    {
    }
}

public class AmqpRpcParamsNotObjectException : Exception
{
    public string code = ErrorCode.AmqpRpcParamsNotObject;
    public AmqpRpcParamsNotObjectException(string message): base(message)
    {
    }
}

public class AmqpRpcParamsMissingPropertiesException : Exception
{
    public string code = ErrorCode.AmqpRpcParamsMissingProperties;
    public AmqpRpcParamsMissingPropertiesException(string message): base(message)
    {
    }
}

public class AmqpRpcUnknowParameterException : Exception
{
    public string code = ErrorCode.AmqpRpcUnknownParameter;
    public AmqpRpcUnknowParameterException(string message): base(message)
    {
    }
}

public class AmqpRpcUnknownFunctionException : Exception
{
    public string code = ErrorCode.AmqpRpcUnknownFunction;
    public AmqpRpcUnknownFunctionException(string message): base(message)
    {
    }
}

public class AmqpRpcFunctionDefinitionValidationException : Exception
{
    public string code = ErrorCode.AmqpRpcFunctionDefinitionValidationError;
    public AmqpRpcFunctionDefinitionValidationException(string message): base(message)
    {
    }
}

public class AmqpRpcInvalidNodeAddressException : Exception
{
    public string code = ErrorCode.AmqpRpcInvalidNodeAddressException;
    public AmqpRpcInvalidNodeAddressException(string message): base(message)
    {
    }
}

public class AmqpRpcInvalidRpcTypeException : Exception
{
    public string code = ErrorCode.AmqpRpcInvalidRpcTypeException;
    public AmqpRpcInvalidRpcTypeException(string message): base(message)
    {
    }
}