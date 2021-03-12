using System.Linq;

namespace AGF2BMP2AGF
{
	internal class ProcessData
	{
		public FileData AgfFile { get; } = new();
		public FileData OutBmpFile { get; } = new();
		public FileData BmpFile { get; } = new();
		public DecodingData Decoding { get; set; }
		public DecodingData Encoding { get; set; }
	}

	internal class FileData
	{
		public string FileName { get; set; }
		public BITMAPFILEHEADER Bmf { get; set; }
		public BITMAPINFOHEADER Bmi { get; set; }
		public AGFHDR AgfHeader { get; set; }
		public ACIFHDR AcifHeader { get; set; }
		//public byte[] Data { get; set; }
		//public byte[] AlphaBuff { get; set; }
		//public byte[] Buff { get; set; }
		//public int PalLength { get; set; }
	}

	internal class DecodingData
	{
		public BITMAPINFOHEADER Bmi { get; }
		public byte[] DecodedData { get; }
		public byte[] AlphaBuff { get; }
		public byte[] EncodedData { get; }
		public RGBQUAD[] PalArray { get; }

		public DecodingData(BITMAPINFOHEADER bmi, byte[] encodedData, RGBQUAD[] pal, byte[] alphaBuff, byte[] decodedData)
		{
			Bmi = bmi;
			EncodedData = encodedData?.ToArray();
			PalArray = pal?.ToArray();
			AlphaBuff = alphaBuff?.ToArray();
			DecodedData = decodedData?.ToArray();
		}
	}
}