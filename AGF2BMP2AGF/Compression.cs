using System.Runtime.InteropServices;

namespace AGF2BMP2AGF
{
	public static class Compression
	{
		private const string LibraryName = "LzssCpp.dll";

		[DllImport(LibraryName, CallingConvention = CallingConvention.StdCall)]
		// ReSharper disable once IdentifierTypo
		private static extern int unlzss(byte[] input, int inputLength, [MarshalAs(UnmanagedType.LPArray)] byte[] output, int outputLength);

		[DllImport(LibraryName, CallingConvention = CallingConvention.StdCall)]
		private static extern int lzss(byte[] input, int inputLength, [MarshalAs(UnmanagedType.LPArray)]byte[] output, int outputLength);
		
		public static byte[] UnpackLZSS(byte[] inputData, int outLen)
		{
				var buff = new byte[outLen];
				unlzss(inputData, inputData.Length, buff, outLen);
				return buff;
		}

		public static byte[] PackLZSS(byte[] inputData, int outLen)
		{
			var buff = new byte[outLen];
			lzss(inputData, inputData.Length, buff, outLen);
			return buff;
		}
	}
}
