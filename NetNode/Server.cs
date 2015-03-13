using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NetNode
{
	public enum ServerStatus
	{
		Stopped = 1,
		Stopping = 2,
		Starting = 3,
		Started = 4
	}

	public struct ServerCallbacks
	{
		public Action<ServerStatus> OnStartError;										// There was an error starting the server. Parameter is the current ServerStatus
		public Action OnStart;															// Called after the server has started
		public Action OnStop;															// When the server is stopped and all sockets are closed
		public Action<SocketPoolEntry, NodePortIPLink> OnSocketConnect;					// When socket is connected. Parameter is the SocketPoolEntry
		public Action<SocketPoolEntry, NodePortIPLink> OnSocketDisconnect;				// When socket is disconnected. Parameter is the SocketPoolEntry
		public Action<SocketPoolEntry, NodePortIPLink> OnSocketBind;					// Called as soon as the a socket is bound to an IP and port
		public Action<SocketPoolEntry, NodePortIPLink> OnSocketUnbind;					// Called when a socket is closed
		public Action OnError;															// A generic error has occured.
		public Action<SocketPoolEntry, NodePortIPLink, SocketError> OnSocketError;		// A socket error occured, parameters are the SocketPoolEntry and the SocketError code that is passed in the exception.
	}

	public partial class Node
	{
		private void d(string s, string t)
		{
			Console.WriteLine("[{0}]: {1}", s, t);
		}

		private struct ServerListenerParameter
		{
			public ServerListenerParameter(Node instance, IPEndPoint endpoint)
			{
				this.instance = instance;
				this.endpoint = endpoint;
			}

			public Node instance;
			public IPEndPoint endpoint;
		}

		private ServerStatus serverStatus = ServerStatus.Stopped;
		private Dictionary<NodePortIPLink, SocketPoolEntry> serverSocketPool = new Dictionary<NodePortIPLink, SocketPoolEntry>(new NodePortIPLinkArrayComparer()); // As of right now, this would be better off as a Hashset, but we need NodePortIPLink to coreleate with SocketPoolEntry and would like to not create a redundency by including it in the structure.
		private ServerCallbacks? serverCallbacks = null;

		private int activeListenerConnections = 0;
		private int potentialOpenListenerSockets = 0;
		private int openListenerSockets = 0;
		private int failedListenerSockets = 0;

		public void SetServerCallbacks(ServerCallbacks serverCallbacks)
		{
			this.serverCallbacks = serverCallbacks;
		}

		public ServerStatus GetServerStatus()
		{
			return serverStatus;
		}

		public int GetActiveServerConnectionCount()
		{
			return activeListenerConnections;
		}

		public int GetOpenServerSocketCount()
		{
			return openListenerSockets;
		}

		public int GetFailedServerSocketCount()
		{
			return failedListenerSockets;
		}

		public void StartServer()
		{
			lock(this)
			{
				d("SERVER", "Server starting");
				if(serverStatus != ServerStatus.Stopped)
				{
					if(serverCallbacks.HasValue && serverCallbacks.Value.OnStartError != null)
					{
						serverCallbacks.Value.OnStartError(serverStatus);
					}
					else
					{
						throw new InvalidOperationException("NetNode server can only be started from a stopped state");
					}
				}
				else if(BindableIPs != null && (potentialOpenListenerSockets = BindableIPs.Count) > 0)
				{
					serverStatus = ServerStatus.Starting;
					foreach(IPEndPoint endpoint in BindableIPs)
					{
						new Thread(ServerListenerThread).Start(new ServerListenerParameter(this, endpoint));
					}
				}
			}
		}

		private object listenerThreadLock = new object(); // Use this for locking the listener threads
		private void ServerListenerThread(object obj)
		{
			ServerListenerParameter param = (ServerListenerParameter)obj;

			d("SERVER", param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " listener starting");

			bool bindEstablished = false;
			SocketPoolEntry? entry = null;
			NodePortIPLink? ipLink = null;
			byte[] endpointAddress = param.endpoint.Address.GetAddressBytes();
			try
			{
				Socket socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

				socketListener.Bind(param.endpoint);
				socketListener.Listen(Filters.ApplyFilter<int>(endpointAddress, param.endpoint.Port, typeof(Filter.MaxPendingQueue), 10));

				ipLink = new NodePortIPLink(param.endpoint.Address.GetAddressBytes(), param.endpoint.Port);
				socketListener.ReceiveTimeout = Filters.ApplyFilter<int>(ipLink.Value, typeof(Filter.SocketPollTimeout), 0);
				socketListener.SendTimeout = socketListener.ReceiveTimeout;

				entry = new SocketPoolEntry(socketListener);
				lock(this)
				{
					serverSocketPool.Add(ipLink.Value, entry.Value);
				}

				if(serverCallbacks.HasValue && serverCallbacks.Value.OnSocketBind != null)
				{
					serverCallbacks.Value.OnSocketBind(entry.Value, ipLink.Value);
				}

				bindEstablished = true;
				lock(listenerThreadLock)
				{
					openListenerSockets++;
					if(openListenerSockets + failedListenerSockets == BindableIPs.Count)
					{
						serverStatus = ServerStatus.Started;

						if(serverCallbacks.HasValue)
						{
							serverCallbacks.Value.OnStart();
						}
					}
				}

				d("SERVER", param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " connection open to incomming connections");

				while(serverStatus == ServerStatus.Starting || serverStatus == ServerStatus.Started)
				{
					d("SERVER", param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " ...waiting to connect...");
					lock(entry.Value.sLock) // Lock accepting connections on this socket until the last connection is finished processing. We don't want our incomming data to be mixed up and cause a failure, or worse.
					{
						try
						{
							Socket incomming = socketListener.Accept(); // Suspect to cause hanging? TODO: Detech a server stop here
							d("SERVER", param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " incomming connection established");

							byte[] buffer = new byte[NodeMagicPayload.Length]; // Expect the magic payload. This will say if the request came from a Node server. We don't check it here though, that's up to the client.
							int bufferSize = incomming.Receive(buffer);
							if(bufferSize == buffer.Length)
							{
								byte[] bufferReverse = new byte[bufferSize]; // Reverse here to verify on the client end
								for(int i = 0;i < bufferSize;i++)
								{
									bufferReverse[i] = buffer[bufferSize - 1 - i];
								}
								bufferSize = incomming.Send(bufferReverse);
								if(bufferSize == bufferReverse.Length)
								{
									ServerSocketProcess(new SocketPoolEntry(incomming), ipLink.Value); // Start active connection with client
								}
							}
						}
						catch(SocketException) // Something happened with the connection. Retry?
						{
							continue;
						}
					}
				}

				openListenerSockets--;
			}
			catch(Exception ex)
			{
				if(bindEstablished)
				{
					openListenerSockets--;
				}
				failedListenerSockets++;

				if(serverCallbacks.HasValue)
				{
					if(serverCallbacks.Value.OnSocketError != null && ipLink.HasValue && ex is SocketException)
					{
						serverCallbacks.Value.OnSocketError(entry.Value, ipLink.Value, ((SocketException)ex).SocketErrorCode);
					}
					else if(serverCallbacks.Value.OnError != null)
					{
						serverCallbacks.Value.OnError();
					}
				}

				if(failedListenerSockets == potentialOpenListenerSockets || Filters.ApplyFilter<bool>(endpointAddress, param.endpoint.Port, typeof(Filter.Essential), false))
				{
					this.StopServer();
				}
			}

			lock(listenerThreadLock)
			{
				if(openListenerSockets == 0)
				{
					serverStatus = ServerStatus.Stopped;

					if(serverCallbacks.HasValue)
					{
						serverCallbacks.Value.OnStop();
					}
				}

				if(ipLink.HasValue)
				{
					lock(this)
					{
						serverSocketPool.Remove(ipLink.Value);
					}
				}
			}
		}

		private void ServerSocketProcess(SocketPoolEntry entry, NodePortIPLink ipLink)
		{
			// Spawn a new thread for established connections.
			new Thread(delegate()
			{
				activeListenerConnections++;

				if(serverCallbacks.HasValue && serverCallbacks.Value.OnSocketConnect != null)
				{
					serverCallbacks.Value.OnSocketConnect(entry, ipLink);
				}

				lock(entry.sLock) // There actually isn't a reason to do this right now. TODO: Remove this lock if still unneeded in future.
				{
					while(serverStatus == ServerStatus.Starting || serverStatus == ServerStatus.Started)
					{
						try
						{
							byte[] buffer = new byte[1];
							int bufferSize = entry.socket.Receive(buffer);
							d("SERVER", "\tProcessing...");
							if(bufferSize == buffer.Length)
							{
								if(buffer[0] == (byte)InitPayloadFlag.Ping)
								{
									entry.socket.Send(buffer);
								}
								else if(buffer[0] == (byte)InitPayloadFlag.FunctionPayload)
								{
									buffer = new byte[4];
									bufferSize = entry.socket.Receive(buffer);
									if(bufferSize == buffer.Length)
									{
										int payloadSize = BitConverter.ToInt32(buffer, 0);
										buffer = new byte[payloadSize];
										bufferSize = entry.socket.Receive(buffer);
										if(bufferSize == buffer.Length)
										{
											d("SERVER", "\tprocessing function...");
											try
											{
												NodePayload payload = new NodePayload(buffer);
												NodeFunc func = GetListener(payload.signature);
												if(func != null)
												{
													// Send back response data
													buffer = func(payload.data);
													if(buffer == null) // No data to send back
													{
														entry.socket.Send(BitConverter.GetBytes((int)0));
													}
													else
													{
														byte[] sizeHeader = BitConverter.GetBytes(buffer.Length);
														bufferSize = entry.socket.Send(sizeHeader);
														if(buffer.Length > 0 && bufferSize == sizeHeader.Length)
														{
															entry.socket.Send(buffer);
														}
													}
												}
											}
											catch(Exception)
											{
												continue; // There was an error during processing. Don't kill the server, just accept it and move on.
											}
										}
									}
								}
								else if(buffer[0] == (byte)InitPayloadFlag.RequestAsServer)
								{
									d("SERVER", "Receiving client as server request");
									buffer = new byte[sizeof(int)];
									bufferSize = entry.socket.Receive(buffer);
									if(bufferSize == buffer.Length)
									{
										int payloadSize = BitConverter.ToInt32(buffer, 0);
										if(payloadSize > 0) // First check if an IP has been sent
										{
											buffer = new byte[payloadSize];
											bufferSize = entry.socket.Receive(buffer);
											if(bufferSize == buffer.Length)
											{
												byte[] ip = buffer;
												// Got the IP address, now get the port
												buffer = new byte[sizeof(int)];
												bufferSize = entry.socket.Receive(buffer);
												if(bufferSize == buffer.Length)
												{
													int port = BitConverter.ToInt32(buffer, 0);

													AddNodeIP(ip, port, NodeIPType.Connectable);
													ClientHotStart();
												}
											}
										}
										else
										{
											// We'll just treat the existing connection as a server instead of creating a new connection.
											// TODO: Do that.
										}
									}
								}
							}
							else
							{
								break;
							}
						}
						catch(SocketException se)
						{
							if(serverCallbacks.HasValue)
							{
								if(serverCallbacks.Value.OnSocketError != null)
								{
									serverCallbacks.Value.OnSocketError(entry, ipLink, se.SocketErrorCode);
								}
								else if(serverCallbacks.Value.OnError != null)
								{
									serverCallbacks.Value.OnError();
								}
							}
							break;
						}
					}
				}

				lock(this)
				{
					// Poll for connection and send disconnect from client if one exists
					if(entry.socket.Poll(Filters.ApplyFilter<int>(ipLink, typeof(Filter.SocketPollTimeout), -1), SelectMode.SelectRead) && entry.socket.Connected)
					{
						entry.socket.Disconnect(false);
					}
				}

				activeListenerConnections--;

				if(serverCallbacks.HasValue && serverCallbacks.Value.OnSocketDisconnect != null)
				{
					serverCallbacks.Value.OnSocketDisconnect(entry, ipLink);
				}
			}).Start();
		}

		public void StopServer()
		{
			lock(this)
			{
				serverStatus = ServerStatus.Stopping;

				foreach(KeyValuePair<NodePortIPLink, SocketPoolEntry> entry in serverSocketPool)
				{
					if(entry.Value.socket != null)
					{
						entry.Value.socket.Close();
						if(serverCallbacks.HasValue && serverCallbacks.Value.OnSocketUnbind != null)
						{
							serverCallbacks.Value.OnSocketUnbind(entry.Value, entry.Key);
						}
					}
				}
				serverSocketPool.Clear();
			}
		}
	}
}
