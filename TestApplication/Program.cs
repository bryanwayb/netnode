using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetNode;
using System.Net.Sockets;
using System.Net;

namespace TestApplication
{
	class Program
	{
		static void Main(String[] args)
		{
			NodeIP ipConfig = new NodeIP();
			bool asServer = false; // Default to client
			for(int i = 0;i < args.Length; i++)
			{
				String currentArg = args[i].Trim().ToLower();

				if(currentArg == "-h" || currentArg == "--host")
				{
					ipConfig.ip = Dns.GetHostEntry(args[++i]).AddressList[0].GetAddressBytes();
				}
				else if(currentArg == "-i" || currentArg == "--ip")
				{
					ipConfig.ip = IPAddress.Parse(args[++i]).GetAddressBytes();
				}
				else if(currentArg == "-p" || currentArg == "--port")
				{
					ipConfig.ports = new int[] { Int32.Parse(args[++i]) };
				}
				else if(currentArg == "-s" || currentArg == "--server")
				{
					asServer = true;
				}
				else if(currentArg == "-c" || currentArg == "--client")
				{
					asServer = false;
				}
			}

			Console.WriteLine("NetNode Example\nRunning with the following configuration...\n------------");

			Console.WriteLine("IP Address: " + ipConfig.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b));
			Console.WriteLine("Port: " + ipConfig.ports[0]);
			Console.WriteLine("Mode: " + (asServer ? "Server" : "Client"));

			if(asServer)
			{
				NetNode.Node.Default.AddListener("ExampleRemoteFunction", delegate(byte[] param)
				{
					Console.WriteLine("This is an example function that was called from the client.");
					return null;
				});

				Node.Default.SetServerCallbacks(new ServerCallbacks()
				{
					OnStartError = delegate(ServerStatus status)
					{
						Console.WriteLine("Server start failed. Client status: " + status.ToString());
					},
					OnStart = delegate()
					{
						Console.WriteLine("Server started");
					},
					OnStop = delegate()
					{
						Console.WriteLine("Server stopped");
					},
					OnSocketConnect = delegate(SocketPoolEntry entry, NodePortIPLink link)
					{
						Console.WriteLine("Server connected to " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
					},
					OnSocketDisconnect = delegate(SocketPoolEntry entry, NodePortIPLink link)
					{
						Console.WriteLine("Server disconnected from " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
					},
					OnError = delegate()
					{
						Console.WriteLine("Server encountered an error");
					},
					OnSocketError = delegate(SocketPoolEntry entry, NodePortIPLink link, SocketError error)
					{
						Console.WriteLine("Server socket error on " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port + ": " + error.ToString());
					},
					OnSocketBind = delegate(SocketPoolEntry entry, NodePortIPLink link)
					{
						Console.WriteLine("Server bound to " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
					},
					OnSocketUnbind = delegate(SocketPoolEntry entry, NodePortIPLink link)
					{
						Console.WriteLine("Server unbound from " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
					}
				});

				Node.Default.AddNodeIP(ipConfig, NodeIPType.Bindable);
				Node.Default.StartServer();
			}
			else
			{
				Node.Default.SetClientCallbacks(new ClientCallbacks()
				{
					OnStartError = delegate(ClientStatus status)
					{
						Console.WriteLine("Client start failed. Client status: " + status.ToString());
					},
					OnStart = delegate()
					{
						Console.WriteLine("Client started");

						Node.Default.ClientExecuteFunction(new NodePortIPLink(ipConfig.ip, ipConfig.ports[0]), "ExampleRemoteFunction", null);
					},
					OnStop = delegate()
					{
						Console.WriteLine("Client stopped");
					},
					OnSocketConnect = delegate(SocketPoolEntry entry, NodePortIPLink link)
					{
						Console.WriteLine("Client connected to " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
					},
					OnSocketDisconnect = delegate(SocketPoolEntry entry, NodePortIPLink link)
					{
						Console.WriteLine("Client disconnected from " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port);
					},
					OnError = delegate()
					{
						Console.WriteLine("Client encountered an error");
					},
					OnSocketError = delegate(SocketPoolEntry entry, NodePortIPLink link, SocketError error)
					{
						Console.WriteLine("Client socket error on " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port + ": " + error.ToString());
					}
				});

				Node.Default.AddNodeIP(ipConfig, NodeIPType.Connectable);
				Node.Default.ClientStart();
			}
		}
	}
}
