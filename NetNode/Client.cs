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
		public Action<ClientStatus> OnStartError;										// There was an error starting the client. Parameter is the current ClientStatus
		public Action OnStart;															// Called after the NetNode client has started
		public Action OnStop;															// When the client is stopped and all sockets are closed
		public Action<SocketPoolEntry, NodePortIPLink> OnSocketConnect;					// When socket is connected. Parameter is the SocketPoolEntry
		public Action<SocketPoolEntry, NodePortIPLink> OnSocketDisconnect;				// When socket is disconnected. Parameter is the SocketPoolEntry
		public Action OnError;															// A generic error has occured. TODO: Perhaps pass a custom error code detailing what when wrong?
		public Action<SocketPoolEntry, NodePortIPLink, SocketError> OnSocketError;		// A socket error occured, parameters are the SocketPoolEntry and the SocketError code that is passed in the exception.
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

		public void ClientStart()
		{
			lock(this)
			{
				d("CLIENT", "Client starting");
				if(clientStatus != ClientStatus.Stopped) // Prevent eating up resouces
				{
					if(clientCallbacks.HasValue && clientCallbacks.Value.OnStartError != null)
					{
						clientCallbacks.Value.OnStartError(clientStatus);
					}
					else
					{
						throw new InvalidOperationException("NetNode client can only be started from a stopped state");
					}
				}
				else if(ConnectableIPs != null && (potentialOpenClientSockets = ConnectableIPs.Count) > 0)
				{
					clientStatus = ClientStatus.Starting;
					foreach(IPEndPoint endpoint in ConnectableIPs)
					{
						new Thread(ClientRunnerThread).Start(new ClientRunnerParameter(this, endpoint));
					}
				}
			}
		}

		public void ClientHotStart() // Same thing as ClientStart, except this will only start endpoints that haven't been started yet.
		{
			// TODO: It might be worth it just to fuse this with the standard ClientStart() function. Maybe have a boolean variable to signal a hot start, or just have it hot start by default. Will decide on this...
			lock(this)
			{
				if(clientStatus == ClientStatus.Started)
				{
					foreach(IPEndPoint endpoint in ConnectableIPs)
					{
						NodePortIPLink ipLink = new NodePortIPLink(endpoint.Address.GetAddressBytes(), endpoint.Port);
						if(!clientSocketPool.ContainsKey(ipLink))
						{
							new Thread(ClientRunnerThread).Start(new ClientRunnerParameter(this, endpoint));
						}
					}
				}
				else if(clientStatus != ClientStatus.Stopped)
				{
					ClientStart();
				}
				else
				{
					// TODO: Error here?
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

				sock.ReceiveTimeout = Filters.ApplyFilter<int>(ipLink.Value, typeof(Filter.SocketPollTimeout), 0);
				sock.SendTimeout = sock.ReceiveTimeout; // TODO: There may be a reason to make these seperate, but until that comes leaving them as one and the same

				entry = new SocketPoolEntry(sock);
				lock(this)
				{
					clientSocketPool.Add(ipLink.Value, entry.Value);
				}
				
				openClientSockets++;
				try
				{
					if(clientCallbacks.HasValue && clientCallbacks.Value.OnSocketConnect != null)
					{
						clientCallbacks.Value.OnSocketConnect(entry.Value, ipLink.Value);
					}
					ClientConnectToNode(ipLink.Value, entry.Value);
				}
				catch(Exception ex)
				{
					openClientSockets--;

					if(clientCallbacks.HasValue)
					{
						if(clientCallbacks.Value.OnSocketError != null && ex is SocketException) // Call this if a socket error handler has been assigned
						{
							clientCallbacks.Value.OnSocketError(entry.Value, ipLink.Value, ((SocketException)ex).SocketErrorCode);
						}
					}

					throw ex;
				}
			}
			catch(Exception ex)
			{
				failedClientSockets++;

				if(entry.HasValue)
				{
					entry.Value.socket.Close();

					if(ipLink.HasValue)
					{
						if(clientCallbacks.HasValue && clientCallbacks.Value.OnSocketDisconnect != null)
						{
							clientCallbacks.Value.OnSocketDisconnect(entry.Value, ipLink.Value);
						}

						lock(this)
						{
							clientSocketPool.Remove(ipLink.Value);
						}
					}
				}

				if(clientCallbacks.Value.OnSocketError == null && !(ex is SocketException) && clientCallbacks.Value.OnError != null) // Try to call generic error handler otherwise.
				{
					clientCallbacks.Value.OnError();
				}

				ClientCheckIfStopNeeded();
			}
		}

		private object clientSocketProcessLock = new object();
		private void ClientSocketProcess(SocketPoolEntry entry, NodePortIPLink ipLink)
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
				int pollTimeout = Filters.ApplyFilter<int>(ipLink, typeof(Filter.SocketPollTimeout), -1);

				// This acts as the heartbeat to the server. Consistantly ping the server to keep the connection alive.
				while(clientStatus == ClientStatus.Starting || clientStatus == ClientStatus.Started)
				{
					try
					{
						lock(entry.sLock)
						{
							if(entry.socket.Poll(pollTimeout, SelectMode.SelectWrite) && entry.socket.Connected) // TODO: Make timeout configurable (in microseconds)
							{
								byte[] buffer = new byte[] { (byte)InitPayloadFlag.Ping };
								if(entry.socket.Send(buffer) == buffer.Length)
								{
									if(entry.socket.Receive(buffer) != buffer.Length)
									{
										break;
									}
								}
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

					if(clientCallbacks.HasValue && clientCallbacks.Value.OnSocketDisconnect != null)
					{
						clientCallbacks.Value.OnSocketDisconnect(entry, ipLink);
					}
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
						ClientSocketProcess(entry, ipLink);
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
					this.ClientStop();
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

		public void ClientStop()
		{
			lock(this)
			{
				clientStatus = ClientStatus.Stopping;

				foreach(KeyValuePair<NodePortIPLink, SocketPoolEntry> entry in clientSocketPool)
				{
					entry.Value.socket.Close();

					if(clientCallbacks.HasValue && clientCallbacks.Value.OnSocketDisconnect != null)
					{
						clientCallbacks.Value.OnSocketDisconnect(entry.Value, entry.Key);
					}
				}
				clientSocketPool.Clear();
			}
		}

		public byte[] ClientExecuteFunction(NodePortIPLink iplink, string signature, byte[] data)
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
				if(clientCallbacks.HasValue && clientCallbacks.Value.OnError != null)
				{
					clientCallbacks.Value.OnError();
				}
				else
				{
					throw new Exception("Socket is not open");
				}
			}

			return null;
		}

		public bool EnableClientAsServer(NodePortIPLink ipLink, NodePortIPLink? ipBind = null)
		{
			if(clientSocketPool.ContainsKey(ipLink))
			{
				SocketPoolEntry entry = clientSocketPool[ipLink];
				lock(entry.sLock)
				{
					d("CLIENT", "Requesting to be server");
					int bufferSize = entry.socket.Send(new byte[] { (byte)InitPayloadFlag.RequestAsServer });
					if(bufferSize == sizeof(byte))
					{
						// Here we'll tell the server either to connected to a specific IP address from where the request came from or if it should use the already open connection.
						byte[] buffer = null;
						if(ipBind.HasValue)
						{
							// First send IP
							buffer = BitConverter.GetBytes(ipBind.Value.ip.Length); // IP size (usually 4 or 6 bytes)
							bufferSize = entry.socket.Send(buffer);
							if(bufferSize == buffer.Length)
							{
								buffer = ipBind.Value.ip;
								bufferSize = entry.socket.Send(buffer); // The actual IP address
								if(bufferSize == buffer.Length)
								{
									// Now send port
									entry.socket.Send(BitConverter.GetBytes(ipBind.Value.port));
								}
							}
						}
						else
						{
							entry.socket.Send(BitConverter.GetBytes(0)); // There's nothing to send here
						}

						// 
					}
				}

				return true;
			}

			return false;
		}
	}
}
