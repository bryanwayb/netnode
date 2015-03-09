using System.Runtime.InteropServices;

namespace NetNode
{
	static class Lib
	{
		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int memcmp(byte[] b1, byte[] b2, long count); // We need this for fast memory comparing
	}
}
