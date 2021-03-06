﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetNode.Filter
{
	public struct KeepAlivePing : Base
	{
		public int Milliseconds;

		public KeepAlivePing(int Milliseconds)
		{
			this.Milliseconds = Milliseconds;
		}

		public T ApplyFilter<T>(object param)
		{
			return (T)Convert.ChangeType(Milliseconds, typeof(T));
		}
	}
}
