using System.Linq;

namespace AGF2BMP2AGF
{
	public class ProcessData
	{
		public FileData AgfFile { get; } = new();
		public DecodingData Decoding { get; set; }
		public DecodingData Encoding { get; set; }
	}

	public class FileData
	{
		public string FileName { get; set; }
		public BITMAPFILEHEADER Bmf { get; set; }
		public BITMAPINFOHEADER Bmi { get; set; }
		public AGFHDR AgfHeader { get; set; }
		public ACIFHDR AcifHeader { get; set; }
	}

	public class DecodingData
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