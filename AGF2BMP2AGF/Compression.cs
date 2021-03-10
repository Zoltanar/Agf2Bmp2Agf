using System.Runtime.InteropServices;

namespace AGF2BMP2AGF
{
	static class Compression
	{
		private const string LibraryName = "LzssCpp.dll"; //"agf2bmp2agfCpp.dll";

		[DllImport(LibraryName, CallingConvention = CallingConvention.StdCall)]
		// ReSharper disable once IdentifierTypo
		private static extern unsafe int unlzss(byte[] input, byte* output, ref int inputLength, ref int outputLength);

		[DllImport(LibraryName, CallingConvention = CallingConvention.StdCall)]
		private static extern unsafe int lzss(byte[] input, byte* output, ref int inputLength, ref int outputLength);
		

		public static unsafe byte[] UnpackLZSS(byte[] buff, ulong len, ulong outLen)
		{
			int len1 = (int)len;
			int outLen1 = (int)outLen;
			var outBuf = new byte[outLen];
			fixed (byte* outPtr = outBuf)
			{
				unlzss(buff, outPtr, ref len1, ref outLen1);
			}
			return outBuf;
		}

		public static unsafe byte[] PackLZSS(byte[] buff, ulong len, ref int outLen)
		{
			int len1 = (int)len;
			var outBuf = new byte[outLen];
			fixed (byte* outPtr = outBuf)
			{
				lzss(buff, outPtr, ref len1, ref outLen);
			}
			return outBuf;
		}
	}
}
