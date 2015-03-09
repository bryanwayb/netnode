using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestApplication
{
	class Program
	{
		static void Main(string[] args)
		{
			NetNode.Filters.AddFilter(new byte[] { 127, 0, 0, 1 }, 9090, new NetNode.Filter.MaxPendingQueue(100));
			NetNode.Node.Default.AddNodeIP(new NetNode.NodeIP(new byte[] { 127, 0, 0, 1 }, new int[] { 9090 }), NetNode.NodeIPType.Bindable);
			NetNode.Node.Default.AddNodeIP(new NetNode.NodeIP(new byte[] { 127, 0, 0, 1 }, new int[] { 9090 }), NetNode.NodeIPType.Connectable);
			NetNode.Node.Default.AddListener("ThisIsATest", delegate(byte[] param)
			{
				Console.WriteLine("Here");
				return null;
			});
			NetNode.Node.Default.StartServer();
			NetNode.Node.Default.StartClient();

			new Thread(delegate()
				{
					Thread.Sleep(2000); // Todo: Create a connection callback so that things like this aren't needed
					Console.WriteLine("Client thread");
					NetNode.Node.Default.ClientExecuteFunction(new NetNode.NodePortIPLink(new byte[] { 127, 0, 0, 1 }, 9090), "ThisIsATest", null);
				}).Start();
		}
	}
}
