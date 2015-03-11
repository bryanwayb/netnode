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

	public struct ClientCallbacks
	{
		public Action OnStart;
		public Action OnStop;
	}

	public partial class Node
	{
		private struct ClientRunnerParameter
		{
			public ClientRunnerParameter(Node instance, IPEndPoint endpoint)
			{
				this.instance = instance;
				this.endpoint = endpoint;
			}

			public Node instance;
			public IPEndPoint endpoint;
		}

		private ClientStatus clientStatus = ClientStatus.Stopped;
		private Dictionary<NodePortIPLink, SocketPoolEntry> clientSocketPool = new Dictionary<NodePortIPLink, SocketPoolEntry>(new NodePortIPLinkArrayComparer());
		private ClientCallbacks? clientCallbacks = null;

		private int potentialOpenClientSockets = 0;
		private int openClientSockets = 0;
		private int failedClientSockets = 0;

		public void SetClientCallbacks(ClientCallbacks clientCallbacks)
		{
			this.clientCallbacks = clientCallbacks;
		}

		public ClientStatus GetClientStatus()
		{
			return clientStatus;
		}

		public int GetOpenClientSocketCount()
		{
			return openClientSockets;
		}

		public int GetFailedClientSocketCount()
		{
			return failedClientSockets;
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
				else if(ConnectableIPs != null && (potentialOpenClientSockets = ConnectableIPs.Count) > 0)
				{
					clientStatus = ClientStatus.Starting;
					foreach(IPEndPoint endpoint in BindableIPs)
					{
						new Thread(ClientRunnerThread).Start(new ClientRunnerParameter(this, endpoint));
					}
				}
			}
		}

		private void ClientRunnerThread(object obj)
		{
			ClientRunnerParameter param = (ClientRunnerParameter)obj;

			d("CLIENT", param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " client starting");

			SocketPoolEntry? entry = null;
			NodePortIPLink? ipLink = null;
			try
			{
				Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				sock.Connect(param.endpoint);

				ipLink = new NodePortIPLink(param.endpoint.Address.GetAddressBytes(), param.endpoint.Port);
				entry = new SocketPoolEntry(sock);
				clientSocketPool.Add(ipLink.Value, entry.Value);
				openClientSockets++;
				try
				{
					ClientConnectToNode(ipLink.Value, entry.Value);
				}
				catch(SocketException)
				{
					openClientSockets--;
					throw new Exception();
				}
			}
			catch(Exception)
			{
				failedClientSockets++;

				if(entry.HasValue)
				{
					entry.Value.socket.Close();

					if(ipLink.HasValue)
					{
						clientSocketPool.Remove(ipLink.Value);
					}
				}

				ClientCheckIfStopNeeded();
			}
		}

		private object clientSocketProcessLock = new object();
		private void ClientSocketProcess(NodePortIPLink ipLink, SocketPoolEntry entry)
		{
			new Thread(delegate()
			{
				lock(clientSocketProcessLock)
				{
					if(failedClientSockets + openClientSockets == potentialOpenClientSockets)
					{
						clientStatus = ClientStatus.Started;

						if(clientCallbacks.HasValue)
						{
							clientCallbacks.Value.OnStart();
						}
					}
				}

				int pingDelay = Filters.ApplyFilter<int>(ipLink, typeof(Filter.KeepAlivePing), 3000);

				// This acts as the heartbeat to the server. Consistantly ping the server to keep the connection alive.
				while(clientStatus != ClientStatus.Stopping)
				{
					try
					{
						lock(entry.sLock)
						{
							bool connected = false;
							if(connected = (entry.socket.Poll(-1, SelectMode.SelectWrite) && entry.socket.Connected)) // TODO: Make timeout configurable (in microseconds)
							{
								byte[] buffer = new byte[] { (byte)InitPayloadFlag.Ping };
								if(entry.socket.Send(buffer) != 1 || entry.socket.Receive(buffer) != 1)
								{
									connected = false;
								}
							}

							if(!connected) // Attempt a reconnect
							{
								entry.isVerified = false;
								ClientConnectToNode(ipLink, entry);
								break;
							}
						}
					}
					catch(SocketException)
					{
						break; // There's not really anything we should do to try and repair the connection at this point. Let's just let it close.
					}

					Thread.Sleep(pingDelay);
				}

				if(entry.socket.Connected)
				{
					entry.socket.Close();
				}

				// The socket is due to be cleaned up at this point.
				openClientSockets--;
				ClientCheckIfStopNeeded();
			}).Start();
		}

		private void ClientConnectToNode(NodePortIPLink ipLink, SocketPoolEntry entry)
		{
			d("CLIENT", "\t" + "sending init");
			int bufferSize = entry.socket.Send(NodeMagicPayload);
			if(bufferSize == NodeMagicPayload.Length)
			{
				byte[] buffer = new byte[bufferSize];
				bufferSize = entry.socket.Receive(buffer);
				if(bufferSize == buffer.Length)
				{
					bool valid = true;
					for(int i = 0;i < bufferSize;i++)
					{
						if(buffer[i] != NodeMagicPayload[bufferSize - 1 - i])
						{
							valid = false;
							break;
						}
					}

					if(valid)
					{
						d("CLIENT", "\t" + "node server verified");

						entry.isVerified = true;
						ClientSocketProcess(ipLink, entry);
					}
				}
			}
		}

		private void ClientCheckIfStopNeeded()
		{
			lock(clientSocketProcessLock)
			{
				if(failedClientSockets == potentialOpenClientSockets)
				{
					this.StopClient();
				}

				if(openClientSockets == 0)
				{
					clientStatus = ClientStatus.Stopped;

					if(clientCallbacks.HasValue)
					{
						clientCallbacks.Value.OnStop();
					}
				}
			}
		}

		public void StopClient()
		{
			lock(this)
			{
				clientStatus = ClientStatus.Stopping;

				foreach(KeyValuePair<NodePortIPLink, SocketPoolEntry> entry in clientSocketPool)
				{
					entry.Value.socket.Close();
				}
				clientSocketPool.Clear();
			}
		}

		/*public byte[] ClientExecuteFunction(NodePortIPLink iplink, string signature, byte[] data)
		{
			if(clientSocketPool.ContainsKey(iplink))
			{
				SocketPoolEntry entry = clientSocketPool[iplink];
				lock(entry.sLock)
				{
					Socket sock = entry.socket;
					int bufferSize = sock.Send(new byte[] { (byte)InitPayloadFlag.FunctionPayload }); // Send request as function to the server
					if(bufferSize == sizeof(byte))
					{
						NodePayload payload = new NodePayload(signature, data, this.Encoder);
						bufferSize = entry.socket.Send(BitConverter.GetBytes(payload.GetSize()));
						if(bufferSize == sizeof(int))
						{
							byte[] buffer = payload.ToByteArray();
							bufferSize = entry.socket.Send(buffer);
							if(bufferSize == buffer.Length)
							{
								d("CLIENT", "sent action");
								buffer = new byte[sizeof(int)];
								bufferSize = entry.socket.Receive(buffer);
								if(bufferSize == buffer.Length)
								{
									d("CLIENT", "\tchecking for response data");
									int responseSize = BitConverter.ToInt32(buffer, 0);
									if(responseSize > 0)
									{
										buffer = new byte[responseSize];
										bufferSize = entry.socket.Receive(buffer);
										if(bufferSize == buffer.Length)
										{
											return buffer;
										}
									}
								}
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
		}*/
	}
}
