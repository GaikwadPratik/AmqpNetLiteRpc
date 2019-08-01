namespace AmqpNetLiteRpcCore
{
  public class MessageOptions: IMessageOptions
  {
	  public uint Timeout { get; set; }
  }

  public interface IMessageOptions
  {
	  uint Timeout { get; set; }
  }
}