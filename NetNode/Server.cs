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

	public partial class Node
	{
		private void d(string s, string t)
		{
			Console.WriteLine("[{0}]: {1}", s, t);
		}

		private ServerStatus serverStatus = ServerStatus.Stopped;
		private List<Thread> serverListenerPool = new List<Thread>();

		private struct ServerListenerParameter
		{
			public ServerListenerParameter(Node instance, IPEndPoint endpoint, Thread thread)
			{
				this.instance = instance;
				this.endpoint = endpoint;
				this.thread = thread;
			}

			public Node instance;
			public IPEndPoint endpoint;
			public Thread thread;
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
				else if(BindableIPs != null && BindableIPs.Count > 0)
				{
					serverStatus = ServerStatus.Starting;
					foreach(IPEndPoint endpoint in BindableIPs)
					{
						Thread listener = new Thread(ServerListenerThread);
						serverListenerPool.Add(listener);
						listener.Start(new ServerListenerParameter(this, endpoint, listener));
					}
				}
			}
		}

		private object listenerThreadLock = new object(); // Use this for locking the listener threads
		private void ServerListenerThread(object obj)
		{
			lock(listenerThreadLock)
			{
				if(serverListenerPool.Count == BindableIPs.Count && serverStatus != ServerStatus.Started)
				{
					serverStatus = ServerStatus.Started;
				}
			}

			ServerListenerParameter param = (ServerListenerParameter)obj;

			d("SERVER", param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " listener starting");

			try
			{
				Socket socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

				socketListener.Bind(param.endpoint);
				socketListener.Listen(Filters.ApplyFilter<int>(param.endpoint.Address.GetAddressBytes(), param.endpoint.Port, typeof(Filter.MaxPendingQueue), 10));
				d("SERVER", param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " connection open to incomming connections");

				while(serverStatus != ServerStatus.Stopping)
				{
					d("SERVER", param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " ...waiting to connect...");
					Socket incomming = socketListener.Accept();
					d("SERVER", param.endpoint.Address.ToString() + ":" + param.endpoint.Port + " incomming connection established");

					byte[] buffer = new byte[NodeMagicPayload.Length];
					int bufferSize = incomming.Receive(buffer);
					if(bufferSize == buffer.Length)
					{
						byte[] bufferReverse = new byte[bufferSize];
						for(int i = 0;i < bufferSize; i++)
						{
							bufferReverse[i] = buffer[bufferSize - 1 - i];
						}
						bufferSize = incomming.Send(bufferReverse);
						if(bufferSize == bufferReverse.Length)
						{
							ServerSocketProcess(incomming);
						}
					}
				}
			}
			catch(Exception ex)
			{
				throw new Exception("Unable to start NetNode listener thread", ex);
			}

			lock(listenerThreadLock)
			{
				serverListenerPool.Remove(param.thread);
				param.thread = null;
				if(serverListenerPool.Count == 0)
				{
					serverStatus = ServerStatus.Stopped;
				}
			}
		}

		private void ServerSocketProcess(Socket sock)
		{
			lock(sock)
			{
				while(serverStatus != ServerStatus.Stopping)
				{
					byte[] buffer = new byte[1];
					int bufferSize = sock.Receive(buffer);
					d("SERVER", "\tProcessing...");
					if(bufferSize == buffer.Length)
					{
						if(buffer[0] == (byte)InitPayloadFlag.Ping)
						{
							sock.Send(buffer);
						}
						else if(buffer[0] == (byte)InitPayloadFlag.FunctionPayload)
						{
							buffer = new byte[4];
							bufferSize = sock.Receive(buffer);
							if(bufferSize == buffer.Length)
							{
								int payloadSize = BitConverter.ToInt32(buffer, 0);
								buffer = new byte[payloadSize];
								bufferSize = sock.Receive(buffer);
								if(bufferSize == buffer.Length)
								{
									d("SERVER", "\tprocessing function...");
									NodePayload payload = new NodePayload(buffer);
									GetListener(payload.signature)(payload.data);
								}
							}
						}
					}
				}
			}
		}
	}
}
