using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetNode.Filter
{
	public struct MaxPendingQueue : Base
	{
		public int MaxConnectionQueue;

		public MaxPendingQueue(int MaxConnectionQueue)
		{
			this.MaxConnectionQueue = MaxConnectionQueue;
		}

		public T ApplyFilter<T>(object param)
		{
			return (T)Convert.ChangeType(MaxConnectionQueue, typeof(T));
		}
	}
}
