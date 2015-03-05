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
			NetNode.Node.Default.AddNodeIP(new NetNode.NodeIP(new byte[] { 127, 0, 0, 1 }, new int[] { 9090, 8081 }));

			foreach(NetNode.NodeIP node in NetNode.Node.Default.GetNodeIP(NetNode.NodeIPType.Bindable))
			{
				Console.Write("Binded IP:\t");
				foreach(byte b in node.ip)
				{
					Console.Write(b.ToString() + " ");
				}
				Console.WriteLine();
				foreach(int i in node.ports)
				{
					Console.WriteLine("\tPort:\t" + i);
				}
				Console.WriteLine();
			}

			NetNode.Node.Default.AddListener("ThisIsATest", delegate(byte[] param)
			{
				Console.WriteLine("Here");
				return null;
			});

			NetNode.Node.Default.StartServer();
		}
	}
}
