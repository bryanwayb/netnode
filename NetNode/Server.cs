﻿using System;
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
		public Action OnStart;
		public Action OnStop;
		public Action OnError;
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
		private HashSet<SocketPoolEntry> serverSocketPool = new HashSet<SocketPoolEntry>();
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
					throw new InvalidOperationException("NetNode server can only be started from a stopped state");
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
			byte[] endpointAddress = param.endpoint.Address.GetAddressBytes();
			try
			{
				Socket socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

				socketListener.Bind(param.endpoint);
				socketListener.Listen(Filters.ApplyFilter<int>(endpointAddress, param.endpoint.Port, typeof(Filter.MaxPendingQueue), 10));

				entry = new SocketPoolEntry(socketListener);
				serverSocketPool.Add(entry.Value);

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

				while(serverStatus != ServerStatus.Stopping)
				{
					d("SERVER", param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " ...waiting to connect...");
					lock(entry.Value.sLock) // Lock accepting connections on this socket until the last connection is finished processing. We don't want our incomming data to be mixed up and cause a failure, or worse.
					{
						try
						{
							Socket incomming = socketListener.Accept();
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
									ServerSocketProcess(new SocketPoolEntry(incomming)); // Start active connection with client
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
			catch(Exception)
			{
				if(bindEstablished)
				{
					openListenerSockets--;
				}
				failedListenerSockets++;

				if(serverCallbacks.HasValue && serverCallbacks.Value.OnError != null)
				{
					serverCallbacks.Value.OnError();
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

				if(entry.HasValue)
				{
					serverSocketPool.Remove(entry.Value);
				}
			}
		}

		private void ServerSocketProcess(SocketPoolEntry entry)
		{
			// Spawn a new thread for established connections.
			new Thread(delegate()
			{
				activeListenerConnections++;
				lock(entry.sLock) // There actually isn't a reason to do this right now. TODO: Remove this lock if still unneeded in future.
				{
					while(serverStatus != ServerStatus.Stopping)
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
							}
							else
							{
								break;
							}
						}
						catch(SocketException se)
						{
							if(se.SocketErrorCode == SocketError.ConnectionAborted || se.SocketErrorCode == SocketError.ConnectionReset)
							{
								break;
							}
							continue;
						}
					}
				}
				activeListenerConnections--;
			}).Start();
		}

		public void StopServer()
		{
			lock(this)
			{
				serverStatus = ServerStatus.Stopping;

				foreach(SocketPoolEntry entry in serverSocketPool)
				{
					entry.socket.Close();
				}
				serverSocketPool.Clear();
			}
		}
	}
}
