using System;
using System.Threading.Tasks;

namespace AmqpNetLiteRpcCore.Util
{
    internal class PendingRequest<TResult> : IPendingRequest
    {
        private readonly TaskCompletionSource<TResult> _tcs;
        private readonly Utility _utility = new Utility();

        public PendingRequest(TaskCompletionSource<TResult> tcs)
        {
            _tcs = tcs;
        }

        public void SetResult(AmqpRpcResponse response)
        {
            if (response.ResponseCode.Equals(RpcResponseType.Ok))
            {
                _tcs.SetResult(this._utility.PeeloutAmqpWrapper(deserializationType: typeof(TResult), response.ResponseMessage));
            }
            else if (response.ResponseCode.Equals(RpcResponseType.Error))
            {
                var _err = this._utility.PeeloutAmqpWrapper(deserializationType: typeof(AmqpRpcServerException), response.ResponseMessage) as AmqpRpcServerException;
                _tcs.SetException(new AmqpRpcException(_err.Message, _err.Stack, _err.Code));
            }
        }

        public void SetError(string error)
        {
            var exception = new Exception(error);
            _tcs.SetException(exception);
        }
    }
}
