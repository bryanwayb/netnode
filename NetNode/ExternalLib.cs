using System.Runtime.InteropServices;

namespace NetNodelet
{
	public partial class Node
	{
		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern int memcmp(byte[] b1, byte[] b2, long count); // We need this for fast memory comparing
	}
}
