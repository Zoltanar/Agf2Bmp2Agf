using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global

namespace AGF2BMP2AGF
{
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct AGFHDR
	{
		//Should be ACGF
		public fixed byte signature[4];
		public uint type;
		public uint unknown;

		public string GetSignature()
		{
			string s;
			fixed (byte* ptr = signature)
			{
				byte[] bytes = new byte[128];
				int index = 0;
				for (byte* counter = ptr; *counter != 0; counter++)
				{
					bytes[index++] = *counter;
				}
				s = Encoding.UTF8.GetString(bytes,0,4);
			}
			return s;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct ACIFHDR
	{
		// Should be ACIF
		public fixed byte signature[4]; 
		public uint type;
		public uint unknown;
		public uint original_length;
		public uint width;

		// ReSharper disable once UnusedMember.Global
		public uint height; public string GetSignature()
		{
			string s;
			fixed (byte* ptr = signature)
			{
				byte[] bytes = new byte[128];
				int index = 0;
				for (byte* counter = ptr; *counter != 0; counter++)
				{
					bytes[index++] = *counter;
				}
				s = Encoding.UTF8.GetString(bytes, 0, 4);
			}
			return s;
		}
	}

	public struct AGFSECTHDR
	{
		public uint original_length;
		// ReSharper disable once NotAccessedField.Global
		public uint original_length2; // why?
		public uint length;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public struct BITMAPFILEHEADER
	{
		public ushort bfType;
		public uint bfSize;
		public ushort bfReserved1;
		public ushort bfReserved2;
		public uint bfOffBits;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct BITMAPINFOHEADER
	{
		public uint biSize;
		public int biWidth;
		public int biHeight;
		public ushort biPlanes;
		public ushort biBitCount;
		public BitmapCompressionMode biCompression;
		public uint biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public uint biClrUsed;
		public uint biClrImportant;

		// ReSharper disable once UnusedMember.Global
		public void Init()
		{
			biSize = (uint)Marshal.SizeOf(this);
		}
	}

	public enum BitmapCompressionMode : uint
	{
		// ReSharper disable UnusedMember.Global
		BI_RGB = 0,
		BI_RLE8 = 1,
		BI_RLE4 = 2,
		BI_BITFIELDS = 3,
		BI_JPEG = 4,
		BI_PNG = 5
		// ReSharper restore UnusedMember.Global
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct RGBQUAD
	{
		public byte rgbBlue;
		public byte rgbGreen;
		public byte rgbRed;
		public byte rgbReserved;
	}
}
