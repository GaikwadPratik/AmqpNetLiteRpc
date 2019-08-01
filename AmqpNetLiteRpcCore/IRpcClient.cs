using System.Threading.Tasks;

namespace AmqpNetLiteRpcCore
{
	public interface IRpcClient
	{
		void Create(IMessageOptions options);

		Task DestroyAsync();
		
		Task NotifyAsync(string functionName, object parameter = null);

		Task<T> CallAsync<T>(string functionName, object parameter = null) where T: class;
	}
}