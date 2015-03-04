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
			NetNodelet.Node.Default.AddNodeIP(new NetNodelet.NodeIP(new byte[] { 127, 0, 0, 1 }, new int[] { 9090 }));
			NetNodelet.Node.Default.AddListener("ThisIsATest", delegate(byte[] param)
			{
				Console.WriteLine("Here");
				return null;
			});
		}
	}
}
