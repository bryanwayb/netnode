using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NetNode
{
	public enum ClientStatus
	{
		Stopped = 1,
		Stopping = 2,
		Starting = 3,
		Started = 4
	}

	public partial class Node
	{
		private ClientStatus clientStatus = ClientStatus.Stopped;
		private List<Thread> clientRunnerPool = new List<Thread>();
		private Dictionary<NodePortIPLink, Socket> clientSocketPool = new Dictionary<NodePortIPLink, Socket>(new NodePortIPLinkArrayComparer());

		private struct ClientRunnerParameter
		{
			public ClientRunnerParameter(Node instance, IPEndPoint endpoint, Thread thread)
			{
				this.instance = instance;
				this.endpoint = endpoint;
				this.thread = thread;
			}

			public Node instance;
			public IPEndPoint endpoint;
			public Thread thread;
		}

		public void StartClient()
		{
			lock(this)
			{
				d("CLIENT", "Client starting");
				if(clientStatus != ClientStatus.Stopped)
				{
					throw new InvalidOperationException("NetNode client can only be started from a stopped state");
				}
				else if(ConnectableIPs != null && ConnectableIPs.Count > 0)
				{
					clientStatus = ClientStatus.Starting;
					foreach(IPEndPoint endpoint in BindableIPs)
					{
						Thread runner = new Thread(ClientRunnerThread);
						clientRunnerPool.Add(runner);
						runner.Start(new ClientRunnerParameter(this, endpoint, runner));
					}
				}
			}
		}

		private object runnerThreadLock = new object(); // Use this for locking the listener threads
		private void ClientRunnerThread(object obj)
		{
			lock(runnerThreadLock)
			{
				if(clientRunnerPool.Count == ConnectableIPs.Count && clientStatus != ClientStatus.Started)
				{
					clientStatus = ClientStatus.Started;
				}
			}

			ClientRunnerParameter param = (ClientRunnerParameter)obj;

			d("CLIENT", param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " client starting");

			try
			{
				d("CLIENT", "\t" + param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " sending init");
				Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				sock.Connect(param.endpoint);

				int bufferSize = sock.Send(NodeMagicPayload);
				if(bufferSize == NodeMagicPayload.Length)
				{
					byte[] buffer = new byte[bufferSize];
					bufferSize = sock.Receive(buffer);
					if(bufferSize == buffer.Length)
					{
						bool valid = true;
						for(int i = 0; i < bufferSize; i++)
						{
							if(buffer[i] != NodeMagicPayload[bufferSize - 1 - i])
							{
								valid = false;
								break;
							}
						}

						if(valid)
						{
							d("CLIENT", "\t" + param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " node server verified");

							ClientSocketProcess(new NodePortIPLink(param.endpoint.Address.GetAddressBytes(), param.endpoint.Port), sock);
						}
					}
				}
			}
			catch(Exception ex)
			{
				throw new Exception("Unable to start NetNode runner thread", ex);
			}

			lock(runnerThreadLock)
			{
				clientRunnerPool.Remove(param.thread);
				param.thread = null;
				if(clientRunnerPool.Count == 0)
				{
					clientStatus = ClientStatus.Stopped;
				}
			}
		}

		private void ClientSocketProcess(NodePortIPLink iplink, Socket sock)
		{
			clientSocketPool.Add(iplink, sock);
			new Thread(delegate() // Send ping to server to keep connection open
			{
				while(sock != null)
				{
					lock(sock)
					{
						d("CLIENT", "sending ping");
						try
						{
							byte[] buffer = new byte[] { (byte)InitPayloadFlag.Ping };
							if(sock.Send(buffer) != 1 || sock.Receive(buffer) != 1)
							{
								break;
							}
						}
						catch // Connection has been broken
						{
							break;
						}
						Thread.Sleep(Filters.ApplyFilter<int>(iplink, typeof(Filter.KeepAlivePing), 3000));
					}
				}

				if(sock != null) // Broke out of loop because for reason other than stopping the client.
				{
					lock(sock)
					{
						if(sock.Connected) // Socket still thinks it's connected.
						{
							sock.Shutdown(SocketShutdown.Both);
							sock.Close();
						}

						if(clientSocketPool.ContainsKey(iplink))
						{
							clientSocketPool.Remove(iplink);
						}
					}
					sock = null;
				}
			}).Start();
		}

		public byte[] ClientExecuteFunction(NodePortIPLink iplink, string signature, byte[] data)
		{
			if(clientSocketPool.ContainsKey(iplink))
			{
				Socket sock = clientSocketPool[iplink];
				lock(sock)
				{
					int bufferSize = sock.Send(new byte[] { (byte)InitPayloadFlag.FunctionPayload }); // Send request as function to the server
					if(bufferSize == sizeof(byte))
					{
						NodePayload payload = new NodePayload(signature, data, this.Encoder);
						bufferSize = clientSocketPool[iplink].Send(BitConverter.GetBytes(payload.GetSize()));
						if(bufferSize == sizeof(int))
						{
							byte[] buffer = payload.ToByteArray();
							bufferSize = clientSocketPool[iplink].Send(buffer);
							if(bufferSize == buffer.Length)
							{
								d("CLIENT", "done");
							}
						}
					}
				}
			}
			else
			{
				throw new Exception("Socket is not open");
			}

			return null;
		}
	}
}
