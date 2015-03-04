using System.Text;

namespace NetNodelet
{
	public struct NodePayload
	{
		public uint flags;				// Flags to be received by a node server
		public byte extraSize;			// Size of extra data.
		public byte[] extra;			// Extra data set for internal use by the Node. Contents are based on the payload flags.
		public uint signatureSize;		// Size of signature
		public byte[] signature;		// Signature identifier, used to identify binded function
		public uint size;				// Size (in bytes) of payload data
		public byte[] data;				// Payload data to pass to NodeFunc
	}

	public partial class Node
	{
		public static Node Default = new Node();

		private Encoding Encoder = Encoding.UTF8;
	}
}
