using System;
using System.Collections.Generic;

namespace NetNode
{
	public static class Filters
	{
		private static Dictionary<NodePortIPLink, List<Filter.Base>> filters = new Dictionary<NodePortIPLink, List<Filter.Base>>(new NodePortIPLinkArrayComparer());

		public static void AddFilter(byte[] ip, int port, Filter.Base filter)
		{
			AddFilter(new NodePortIPLink(ip, port), filter);
		}

		public static void AddFilter(NodePortIPLink store, Filter.Base filter)
		{
			List<Filter.Base> storesFilters = null;

			if(!filters.ContainsKey(store))
			{
				filters.Add(store, (storesFilters = new List<Filter.Base>()));
			}
			else
			{
				storesFilters = filters[store];
			}

			foreach(Filter.Base b in storesFilters)
			{
				if(b.GetType().Equals(filter.GetType()))
				{
					return; // TODO: Maybe throw an exception here?
				}
			}

			storesFilters.Add(filter);
		}

		public static void RemoveFilter(byte[] ip, int port, Type filterType)
		{
			RemoveFilter(new NodePortIPLink(ip, port), filterType);
		}

		public static void RemoveFilter(NodePortIPLink store, Type filterType)
		{
			if(filters.ContainsKey(store))
			{
				List<Filter.Base> storesFilters = filters[store];
				for(int i = 0; i < storesFilters.Count; i++)
				{
					if(storesFilters[i].GetType().Equals(filterType))
					{
						filters[store].RemoveAt(i);
						break;
					}
				}
			}
		}

		public static T ApplyFilter<T>(byte[] ip, int port, Type filterType, T defaultValue)
		{
			return ApplyFilter(new NodePortIPLink(ip, port), filterType, null, defaultValue);
		}

		public static T ApplyFilter<T>(byte[] ip, int port, Type filterType, object parameter, T defaultValue)
		{
			return ApplyFilter(new NodePortIPLink(ip, port), filterType, parameter, defaultValue);
		}

		public static T ApplyFilter<T>(NodePortIPLink store, Type filterType, T defaultValue)
		{
			return ApplyFilter(store, filterType, null, defaultValue);
		}

		public static T ApplyFilter<T>(NodePortIPLink store, Type filterType, object parameter, T defaultValue)
		{
			if(filters.ContainsKey(store))
			{
				foreach(Filter.Base b in filters[store])
				{
					if(b.GetType().Equals(filterType))
					{
						return b.ApplyFilter<T>(parameter);
					}
				}
			}

			return defaultValue;
		}
	}
}
