using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApplication
{
	class Program
	{
		static void Main(string[] args)
		{
			NetNodelet.Node.Default.AddNodeIP(new NetNodelet.NodeIP(new byte[] { 127, 0, 0, 1 }, new int[] { 9090, 8081 }));

			foreach(int i in NetNodelet.Node.Default.GetNodeIP(new byte[] { 127, 0, 0, 1 }, NetNodelet.NodeIPType.Bindable).ports)
			{
				Console.WriteLine(i);
			}

			foreach(NetNodelet.NodeIP node in NetNodelet.Node.Default.GetNodeIP(NetNodelet.NodeIPType.Bindable))
			{
				foreach(byte b in node.ip)
				{
					Console.Write(b.ToString() + " ");
				}
				Console.WriteLine();
				foreach(int i in node.ports)
				{
					Console.WriteLine(i);
				}
			}

			NetNodelet.Node.Default.AddListener("ThisIsATest", delegate(byte[] param)
			{
				Console.WriteLine("Here");
				return null;
			});
		}
	}
}
