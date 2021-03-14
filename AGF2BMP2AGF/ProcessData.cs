using System.Linq;

namespace AGF2BMP2AGF
{
	public class ProcessData
	{
		public DecodingData Decoding { get; set; }
		public DecodingData Encoding { get; set; }
	}
	
	public class DecodingData
	{
		public BITMAPFILEHEADER Bmf { get; }
		public BITMAPINFOHEADER Bmi { get; }
		public byte[] DecodedData { get; }
		public byte[] AlphaBuff { get; }
		public byte[] EncodedData { get; }
		public RGBQUAD[] PalArray { get; }
		public AGFHDR AgfHeader { get; }
		public ACIFHDR AcifHeader { get; }

		public DecodingData(BITMAPFILEHEADER bmf, BITMAPINFOHEADER bmi, byte[] encodedData, RGBQUAD[] pal, byte[] alphaBuff, byte[] decodedData, AGFHDR agfHeader, ACIFHDR acifHeader)
			: this(bmf, bmi, encodedData, pal, alphaBuff, decodedData)
		{
			AgfHeader = agfHeader;
			AcifHeader = acifHeader;
		}

		public DecodingData(BITMAPFILEHEADER bmf, BITMAPINFOHEADER bmi, byte[] encodedData, RGBQUAD[] pal, byte[] alphaBuff, byte[] decodedData)
		{
			Bmf = bmf;
			Bmi = bmi;
			EncodedData = encodedData?.ToArray();
			PalArray = pal?.ToArray();
			AlphaBuff = alphaBuff?.ToArray();
			DecodedData = decodedData?.ToArray();
		}
	}
}