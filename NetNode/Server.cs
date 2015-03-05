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
		private ServerStatus serverStatus = ServerStatus.Stopped;
		private List<Thread> serverListenerPool = new List<Thread>();

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

		public void StartServer() // Starts the server listen threads
		{
			lock(this)
			{
				if(serverStatus != ServerStatus.Stopped)
				{
					// TODO: Throw an exception here
				}
				if(BindableIPs != null && BindableIPs.Count > 0)
				{
					serverStatus = ServerStatus.Starting;
					foreach(IPEndPoint endpoint in BindableIPs)
					{
						Thread listener = new Thread(ServerListenerThread);
						serverListenerPool.Add(listener);
						listener.Start(new ServerListenerParameter(this, endpoint));

						/*serverWorkerPool.Add(new Thread(delegate(object ip)
						{
							Socket socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); // TODO: Work on this here

							try
							{
								socketListener.Bind((IPEndPoint)ip);
								socketListener.Listen(10);
								Console.WriteLine("Waiting for connection...");

								Socket handler = socketListener.Accept();

								byte[] bytes = new byte[1024];
								handler.Receive(bytes);

								Console.WriteLine("Text received : {0}", bytes);
							}
							catch(Exception ex)
							{
								Console.WriteLine("Error: " + ex.ToString());
							}
						}));*/
					}
				}
			}
		}

		private object listenerThreadLock = new object(); // Use this for locking the listener threads
		private void ServerListenerThread(object obj)
		{
			Console.WriteLine("Starting server listener...");

			lock(listenerThreadLock)
			{
				if(serverListenerPool.Count == BindableIPs.Count && serverStatus != ServerStatus.Started)
				{
					serverStatus = ServerStatus.Started;
				}
			}
			
			ServerListenerParameter param = (ServerListenerParameter)obj;

			Socket socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); // TODO: Work on this here

			try
			{
				socketListener.Bind(param.endpoint);
				socketListener.Listen(10);

				while(serverStatus != ServerStatus.Stopping)
				{
					Console.WriteLine("waiting...");
					Socket incomming = socketListener.Accept();

					// This is test code.
					byte[] b = new byte[102400];
					int size = incomming.Receive(b);
					String t = param.instance.Encoder.GetString(b, 0, size);
					Console.WriteLine(t);
				}
			}
			catch { } // TODO: Error handling
		}

		public ServerStatus GetServerStatus()
		{
			return serverStatus;
		}
	}
}
