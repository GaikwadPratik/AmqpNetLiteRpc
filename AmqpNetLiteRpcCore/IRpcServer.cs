using System.Threading.Tasks;

namespace AmqpNetLiteRpcCore
{
	public interface IRpcServer
	{
		void Create();

		Task DestroyAsync();

		void Bind();

    void Bind(string functionName, RpcRequestObjectType requestObjectType);
	}
}