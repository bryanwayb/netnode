using System.Text;

namespace NetNode
{
	public struct NodePayload
	{
		public uint flags;				// Flags to be received by a node server
		public uint extraSize;			// Size of extra data.
		public byte[] extra;			// Extra data set for internal use by the Node. Contents are based on the payload flags.
		public uint signatureSize;		// Size of signature
		public byte[] signature;		// Signature identifier, used to identify binded function
		public uint size;				// Size (in bytes) of payload data
		public byte[] data;				// Payload data to pass to NodeFunc
	}

	public partial class Node
	{
		public static Node Default = new Node();

		private static byte[] PayloadMagic = new byte[4] { (byte)'n', (byte)'o', (byte)'d', (byte)'e' }; // Required to be the very first 4 bytes of every request.
		
		private Encoding Encoder = Encoding.UTF8;
		
		public void SetEncoder(Encoding encoder)
		{
			this.Encoder = encoder;
		}
		public Encoding GetEncoder()
		{
			return Encoder;
		}
	}
}