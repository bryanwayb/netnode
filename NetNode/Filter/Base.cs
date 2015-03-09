using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetNode.Filter
{
	public interface Base
	{
		T ApplyFilter<T>(object param);
	}
}
