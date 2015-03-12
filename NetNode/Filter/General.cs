using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetNode.Filter
{
	public struct Essential : Base
	{
		public bool ImportantConnection;

		public Essential(bool ImportantConnection)
		{
			this.ImportantConnection = ImportantConnection;
		}

		public T ApplyFilter<T>(object param)
		{
			return (T)Convert.ChangeType(ImportantConnection, typeof(T));
		}
	}

	public struct ConnectionTimeout : Base
	{
		public int Milliseconds;

		public ConnectionTimeout(int Milliseconds)
		{
			this.Milliseconds = Milliseconds;
		}

		public T ApplyFilter<T>(object param)
		{
			return (T)Convert.ChangeType(Milliseconds, typeof(T));
		}
	}

	public struct SocketPollTimeout : Base
	{
		public int Microseconds;

		public SocketPollTimeout(int Microseconds)
		{
			this.Microseconds = Microseconds;
		}

		public T ApplyFilter<T>(object param)
		{
			return (T)Convert.ChangeType(Microseconds, typeof(T));
		}
	}
}
