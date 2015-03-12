using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetNode;
using System.Net.Sockets;

namespace TestApplication
{
	class Program
	{
		static void Main(string[] args)
		{
			NodeIP localIP = new NodeIP(new byte[] { 127, 0, 0, 1 }, 9090);
			NodePortIPLink localIPPortLink = new NodePortIPLink(localIP.ip, 9090);

			// Setup server
			Node.Default.AddNodeIP(localIP, NodeIPType.Bindable);
			Filters.AddFilter(localIPPortLink, new NetNode.Filter.Essential(true));
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
					Console.WriteLine("Server connected to " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b));
				},
				OnSocketDisconnect = delegate(SocketPoolEntry entry, NodePortIPLink link)
				{
					Console.WriteLine("Server disconnected from " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b));
				},
				OnError = delegate()
				{
					Console.WriteLine("Server encountered an error");
				},
				OnSocketError = delegate(SocketPoolEntry entry, NodePortIPLink link, SocketError error)
				{
					Console.WriteLine("Server socket error on " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b) + ":" + link.port + ": " + error.ToString());
				}
			});
			NetNode.Node.Default.AddListener("ThisIsATest", delegate(byte[] param)
			{
				Console.WriteLine("This is a remote function being executed on the server");
				return null;
			});
			Node.Default.StartServer();

			// Setup the client
			Node.Default.AddNodeIP(localIP, NodeIPType.Connectable);
			Node.Default.SetClientCallbacks(new ClientCallbacks()
			{
				OnStartError = delegate(ClientStatus status)
				{
					Console.WriteLine("Client start failed. Client status: " + status.ToString());
				},
				OnStart = delegate()
				{
					Console.WriteLine("Client started");

					Node.Default.ClientExecuteFunction(localIPPortLink, "ThisIsATest", null); // Executes remote function
					
					new Thread(delegate()
					{
						Thread.Sleep(5000); // Sleep for 5 seconds
						Node.Default.StopServer(); // Then stop the server. This will cause the client to lose connectivity (but it won't stop the client).
					}).Start();
				},
				OnStop = delegate()
				{
					Console.WriteLine("Client stopped");
				},
				OnSocketConnect = delegate(SocketPoolEntry entry, NodePortIPLink link)
				{
					Console.WriteLine("Client connected to " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b));
				},
				OnSocketDisconnect = delegate(SocketPoolEntry entry, NodePortIPLink link)
				{
					Console.WriteLine("Client disconnected from " + link.ip.Select(s => s.ToString()).Aggregate((a, b) => a + "." + b));
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
			Node.Default.StartClient();
		}
	}
}
