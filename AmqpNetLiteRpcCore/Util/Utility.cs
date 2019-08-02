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
}