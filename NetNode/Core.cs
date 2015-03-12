using System;
using System.Net.Sockets;
using System.Text;

namespace NetNode
{
	public struct NodePayload
	{
		public NodePayload(string signature, byte[] data, Encoding enc)
		{
			this.data = data;
			this.size = this.data != null ? this.data.Length : 0;
			this.signature = enc.GetBytes(signature);
			this.signatureSize = this.signature != null ? this.signature.Length : 0;
		}

		public NodePayload(byte[] payload)
		{
			int position = 0;
			this.signatureSize = BitConverter.ToInt32(payload, position);
			position += sizeof(int);

			this.signature = new byte[this.signatureSize];
			for(int i = 0;i < this.signatureSize;i++, position++)
			{
				this.signature[i] = payload[position];
			}

			this.size = BitConverter.ToInt32(payload, position);
			position += sizeof(int);

			this.data = new byte[this.size];
			for(int i = 0;i < this.size;i++, position++)
			{
				this.data[i] = payload[position];
			}
		}

		public int GetSize()
		{
			return (sizeof(int) * 2) + signatureSize + size;
		}

		public byte[] ToByteArray()
		{
			byte[] buffer = new byte[GetSize()];
			int i = 0;

			byte[] toByte = BitConverter.GetBytes(signatureSize);
			for(int o = 0;o < toByte.Length;i++, o++)
			{
				buffer[i] = toByte[o];
			}

			if(signature != null)
			{
				for(int o = 0;o < signature.Length;i++, o++)
				{
					buffer[i] = signature[o];
				}
			}

			toByte = BitConverter.GetBytes(size);
			for(int o = 0;o < toByte.Length;i++, o++)
			{
				buffer[i] = toByte[o];
			}

			if(data != null)
			{
				for(int o = 0;o < data.Length;i++, o++)
				{
					buffer[i] = data[o];
				}
			}

			return buffer;
		}

		public int signatureSize;		// Size of signature
		public byte[] signature;		// Signature identifier, used to identify binded function
		public int size;				// Size (in bytes) of payload data
		public byte[] data;				// Payload data to pass to NodeFunc
	}

	public enum InitPayloadFlag : byte // These are actions that are to be executed on a node server.
	{
		Ping = 0x0,				// Ping connectivity, does essentially nothing aside from an echo of 0x0.
		FunctionPayload = 0x1,	// Performs function on the server
	}

	public struct SocketPoolEntry
	{
		public SocketPoolEntry(Socket socket)
		{
			this.socket = socket;
			sLock = new object();
			isVerified = false;
		}

		public Socket socket;
		public object sLock;
		public bool isVerified; // Use by the client portion of NetNode
	}

	public partial class Node
	{
		public static Node Default = new Node();

		private static byte[] NodeMagicPayload = new byte[4] { (byte)'n', (byte)'o', (byte)'d', (byte)'e' };
		
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