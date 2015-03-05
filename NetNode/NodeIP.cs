using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NetNodelet
{
	public struct NodeIP
	{
		public NodeIP(byte[] ip, int port)
		{
			this.ip = ip;
			this.ports = new int[] { port };
		}

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

		public void AddNodeIP(byte[] ipAddress, int port)
		{
			AddNodeIP(new NodeIP(ipAddress, port));
		}

		public void AddNodeIP(byte[] ipAddress, int port, NodeIPType type)
		{
			AddNodeIP(new NodeIP(ipAddress, port), type);
		}

		public void AddNodeIP(byte[] ipAddress, int[] ports)
		{
			AddNodeIP(new NodeIP(ipAddress, ports));
		}

		public void AddNodeIP(byte[] ipAddress, int[] ports, NodeIPType type)
		{
			AddNodeIP(new NodeIP(ipAddress, ports), type);
		}

		public void AddNodeIP(NodeIP ip)
		{
			AddNodeIP(ip, NodeIPType.Bindable);
		}

		public void AddNodeIP(NodeIP ip, NodeIPType type)
		{
			List<IPEndPoint> ipList = GetRelevantIPList(type);
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

		public NodeIP GetNodeIP(byte[] ip)
		{
			return GetNodeIP(ip, NodeIPType.Bindable);
		}

		public NodeIP GetNodeIP(byte[] ip, NodeIPType type)
		{
			List<IPEndPoint> ipList = GetRelevantIPList(type);

			List<int> ports = new List<int>();
			if(ip != null)
			{
				foreach(IPEndPoint endpoint in ipList)
				{
					byte[] epIP = endpoint.Address.GetAddressBytes();
					if(epIP.Length == ip.Length && memcmp(epIP, ip, epIP.Length) == 0)
					{
						bool addable = true;
						foreach(int port in ports)
						{
							if(endpoint.Port == port)
							{
								addable = false;
							}
						}

						if(addable)
						{
							ports.Add(endpoint.Port);
						}
					}
				}
			}

			return new NodeIP(ip, ports.ToArray());
		}

		public NodeIP[] GetNodeIP()
		{
			return GetNodeIP(NodeIPType.Bindable);
		}

		public NodeIP[] GetNodeIP(NodeIPType type)
		{
			List<IPEndPoint> ipList = GetRelevantIPList(type);
			List<NodeIP> ret = new List<NodeIP>();

			foreach(IPEndPoint endpoint in ipList)
			{
				byte[] epIP = endpoint.Address.GetAddressBytes();

				bool addable = true;
				foreach(NodeIP node in ret)
				{
					if(node.ip.Length == epIP.Length && memcmp(node.ip, epIP, epIP.Length) == 0)
					{
						addable = false;
						break;
					}
				}

				if(addable)
				{
					ret.Add(GetNodeIP(epIP, type));
				}
			}

			return ret.ToArray();
		}

		public void RemoveNodeIP(byte[] ip)
		{
			RemoveNodeIP(ip, NodeIPType.Bindable);
		}

		public void RemoveNodeIP(byte[] ip, NodeIPType type)
		{
			RemoveNodeIP(new NodeIP(ip, null), type);
		}

		public void RemoveNodeIP(byte[] ip, int port)
		{
			RemoveNodeIP(ip, port, NodeIPType.Bindable);
		}

		public void RemoveNodeIP(byte[] ip, int port, NodeIPType type)
		{
			RemoveNodeIP(new NodeIP(ip, port), type);
		}

		public void RemoveNodeIP(byte[] ip, int[] ports)
		{
			RemoveNodeIP(ip, ports, NodeIPType.Bindable);
		}

		public void RemoveNodeIP(byte[] ip, int[] ports, NodeIPType type)
		{
			RemoveNodeIP(new NodeIP(ip, ports), type);
		}

		public void RemoveNodeIP(NodeIP ip)
		{
			RemoveNodeIP(ip, NodeIPType.Bindable);
		}

		public void RemoveNodeIP(NodeIP ip, NodeIPType type)
		{
			List<IPEndPoint> ipList = GetRelevantIPList(type);
			if(ip.ip != null)
			{
				bool portsSpecified = ip.ports != null && ip.ports.Length > 0;
				for(int i = 0;i < ipList.Count;)
				{
					byte[] epIP = ipList[i].Address.GetAddressBytes();
					if(epIP.Length == ip.ip.Length && memcmp(epIP, ip.ip, epIP.Length) == 0)
					{
						bool remove = !portsSpecified;

						if(portsSpecified)
						{
							foreach(int port in ip.ports)
							{
								if(port == ipList[i].Port)
								{
									remove = true;
									break;
								}
							}
						}

						if(remove)
						{
							ipList.RemoveAt(i);
							continue;
						}
					}

					i++;
				}
			}
		}

		private List<IPEndPoint> GetRelevantIPList(NodeIPType type)
		{
			return type == NodeIPType.Bindable ? BindableIPs : ConnectableIPs;
		}
	}
}
