namespace AmqpNetLiteRpcCore.Util
{
    internal interface IPendingRequest
    {
        void SetResult(AmqpRpcResponse response);
        void SetError(string error);
    }
}
