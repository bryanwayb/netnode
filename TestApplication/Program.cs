using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetNode;

namespace TestApplication
{
	class Program
	{
		static void Main(string[] args)
		{
			NodeIP localIP = new NodeIP(new byte[] { 127, 0, 0, 1 }, 9090);

			// Setup server
			Node.Default.AddNodeIP(localIP, NodeIPType.Bindable);
			Filters.AddFilter(new NodePortIPLink(localIP.ip, 9090), new NetNode.Filter.Essential(true));
			Node.Default.SetServerCallbacks(new ServerCallbacks()
				{
					OnStart = delegate()
					{
						Console.WriteLine("Server started");
						Console.WriteLine(Node.Default.GetOpenServerSocketCount());
						//Node.Default.StopServer();
					},
					OnError = delegate()
					{
						Console.WriteLine("Error");
					},
					OnStop = delegate()
					{
						Console.WriteLine("Server closed");
						Console.WriteLine(Node.Default.GetOpenServerSocketCount());
					}
				});
			Node.Default.StartServer();

			// Setup client
			Node.Default.AddNodeIP(localIP, NodeIPType.Connectable);
			Node.Default.SetClientCallbacks(new ClientCallbacks()
			{
				OnStart = delegate()
				{
					Console.WriteLine("Client started");
					Console.WriteLine(Node.Default.GetOpenClientSocketCount());
					//Node.Default.StopClient();
				},
				OnStop = delegate()
				{
					Console.WriteLine("Client closed");
					Console.WriteLine(Node.Default.GetOpenClientSocketCount());
				}
			});
			Node.Default.StartClient();
		}
	}
}
