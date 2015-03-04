using System.Collections.Generic;
using System.Net;

namespace NetNodelet
{
	public struct NodeIP
	{
		public NodeIP(byte[] ip, int[] ports)
		{
			this.ip = ip;
			this.ports = ports;
		}

		public byte[] ip;		// An IP address that will be used for binding the relating ports
		public int[] ports;		// Array of ports that are to be binded on the relating IP address
	}

	public enum NodeIPType
	{
		Bindable = 1,
		Connectable = 2
	}

	public partial class Node
	{
		private List<IPEndPoint> BindableIPs = new List<IPEndPoint>();
		private List<IPEndPoint> ConnectableIPs = new List<IPEndPoint>();

		public void AddNodeIP(NodeIP ip)
		{
			AddNodeIP(ip, NodeIPType.Bindable);
		}

		public void AddNodeIP(NodeIP ip, NodeIPType type)
		{
			List<IPEndPoint> ipList = type == NodeIPType.Bindable ? BindableIPs : ConnectableIPs;
			if(ip.ip != null && ip.ports != null && ip.ports.Length > 0)
			{
				bool alreadyExists = false;
				foreach(int port in ip.ports) // Add all the ports in the given NodeIP structure
				{
					if(!alreadyExists)
					{
						foreach(IPEndPoint endpoint in ipList)
						{
							byte[] epIP = endpoint.Address.GetAddressBytes();
							alreadyExists = epIP.Length == ip.ip.Length && memcmp(epIP, ip.ip, epIP.Length) == 0;

							if(alreadyExists)
							{
								break;
							}
						}

						ipList.Add(new IPEndPoint(new IPAddress(ip.ip), port));
						alreadyExists = true; // Set this to avoid having to detect the IP address that was just entered into the pool.
					}
					else
					{
						bool portExists = false;
						foreach(IPEndPoint endpoint in ipList)
						{
							byte[] epIP = endpoint.Address.GetAddressBytes();
							if(portExists = epIP.Length == ip.ip.Length && memcmp(epIP, ip.ip, epIP.Length) == 0 && endpoint.Port == port)
							{
								break;
							}
						}

						if(!portExists)
						{
							ipList.Add(new IPEndPoint(new IPAddress(ip.ip), port));
						}
					}
				}
			}
		}
	}
}
