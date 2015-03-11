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
}
