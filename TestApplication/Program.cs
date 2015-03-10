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
			Node.Default.AddNodeIP(localIP, NodeIPType.Bindable);
			Node.Default.StartServer();

			/*NetNode.Filters.AddFilter(new byte[] { 127, 0, 0, 1 }, 9090, new NetNode.Filter.MaxPendingQueue(100));
			NetNode.Node.Default.AddNodeIP(new NetNode.NodeIP(new byte[] { 127, 0, 0, 1 }, new int[] { 9090 }), NetNode.NodeIPType.Bindable);
			NetNode.Node.Default.AddNodeIP(new NetNode.NodeIP(new byte[] { 127, 0, 0, 1 }, new int[] { 9090 }), NetNode.NodeIPType.Connectable);
			NetNode.Node.Default.AddListener("ThisIsATest", delegate(byte[] param)
			{
				Console.WriteLine("Here");
				return null;
			});
			NetNode.Node.Default.StartServer();

			NetNode.Node.Default.StartClient(new NetNode.ClientCallbacks()
			{
				OnStart = delegate()
				{
					NetNode.Node.Default.ClientExecuteFunction(new NetNode.NodePortIPLink(new byte[] { 127, 0, 0, 1 }, 9090), "ThisIsATest", null);
				},
				OnStop = delegate()
				{
					Console.WriteLine("here");
				}
			});*/
		}
	}
}
