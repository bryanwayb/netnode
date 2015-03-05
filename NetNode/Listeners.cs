using System.Collections;

namespace NetNode
{
	public delegate byte[] NodeFunc(byte[] param);

	public partial class Node
	{
		private Hashtable BindPool = new Hashtable();

		public void AddListener(byte[] signature, NodeFunc function)
		{
			AddListener(Encoder.GetString(signature), function);
		}

		public void AddListener(string signature, NodeFunc function)
		{
			BindPool.Add(signature, function);
		}

		public NodeFunc GetListener(byte[] signature)
		{
			return GetListener(Encoder.GetString(signature));
		}

		public NodeFunc GetListener(string signature)
		{
			return (NodeFunc)BindPool[signature];
		}

		public void RemoveListener(byte[] signature)
		{
			BindPool.Remove(Encoder.GetString(signature));
		}

		public void RemoveListener(string signature)
		{
			BindPool.Remove(signature);
		}
	}
}
