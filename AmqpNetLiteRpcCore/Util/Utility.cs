using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amqp;
using Amqp.Serialization;
using Serilog;

namespace AmqpNetLiteRpcCore
{
    public class AmqpRpcNode
    {
        public string Address { get; set; }
        public string Subject { get; set; }
    }

    public static class Extensions
    {
        //https://stackoverflow.com/a/22078975/2398353
        public static async Task<TResult> TimeoutAfterAsync<TResult>(this Task<TResult> task, int timeout)
        {
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask.Equals(task))
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                }
                else
                {
                    throw new TimeoutException();
                }
            }
        }
    }

    public class Utility
    {
        /// <summary>
        /// Converts parameter object from Amqp request to a particular type to be used as input parameter to RPC method being invoked
        /// </summary>
        /// <param name="deserializationType">Type into which object parameters must be converted</param>
        /// <param name="parameters">Input received at AmqpRequest.body.params</param>
        /// <returns>Deserialized object which will be used as an input to Rpc Method</returns>
        public dynamic PeeloutAmqpWrapper(Type deserializationType, object parameters)
        {
            try
            {
                if (deserializationType == null)
                {
                    return null;
                }
                //Create Amqp serializer instance
                AmqpSerializer _serializer = new AmqpSerializer();
                //Create dynamic buffer
                ByteBuffer _paramsBuffer = new ByteBuffer(1024, true);
                //Write object to buffer
                _serializer.WriteObject(_paramsBuffer, parameters);
                //Get ReadObject methodinfo using reflection 
                var _readObjectMethodInfo = typeof(AmqpSerializer).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.Name.Equals("ReadObject") && x.IsGenericMethod && x.GetGenericArguments().Length.Equals(1))
                    .FirstOrDefault();
                if (_readObjectMethodInfo == null)
                {
                    throw new MissingMethodException("ReadObject from AmqpSerializer");
                }
                //Mark methodinfo as generic
                var _readObjectGenericMethodInfo = _readObjectMethodInfo.MakeGenericMethod(deserializationType);
                //Invoke ReadObject to deserialize object
                return _readObjectGenericMethodInfo.Invoke(_serializer, new[] { _paramsBuffer });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception while deserializing request parameters", parameters);
            }
            return null;
        }
    }
}