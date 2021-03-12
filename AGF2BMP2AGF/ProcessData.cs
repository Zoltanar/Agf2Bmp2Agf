using System.Collections.Generic;
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

		// ReSharper disable once UnusedMember.Global
		public bool Compare(int stage = 0)
		{
			return true;
			/*int wrongCount = 0;
			if (!OutBmpFile.FileName.Equals(BmpFile.FileName)) wrongCount++;
			if (!OutBmpFile.Bmf.Equals(BmpFile.Bmf)) wrongCount++;
			if (!OutBmpFile.Bmi.Equals(BmpFile.Bmi)) wrongCount++;
			if (!OutBmpFile.Data.SequenceEqual(BmpFile.Data)) wrongCount++;
			if (stage > 0 && !CompareCollection(OutBmpFile.AlphaBuff, AgfFile.AlphaBuff)) wrongCount++;
			if (stage > 0 && !CompareCollection(OutBmpFile.Buff, AgfFile.Data)) wrongCount++;
			return wrongCount == 0;*/
		}

		// ReSharper disable once UnusedMember.Global
		public bool CompareMapCoding(int stage = 0)
		{
			return true;
			/*int wrongCount = 0;
			//if (!Encoding.Bmi.Equals(Decoding.Bmi)) wrongCount++;
			if (stage > 0 && !CompareCollection(Encoding.DecodedData, Decoding.DecodedData)) wrongCount++;
			if (stage > 0 && !CompareCollection(Encoding.AlphaBuff, Decoding.AlphaBuff)) wrongCount++;
			if (stage > 0 && !CompareCollection(Encoding.PalArray, Decoding.PalArray)) wrongCount++;
			if (stage > 0 && !CompareCollection(Encoding.EncodedData, Decoding.EncodedData)) wrongCount++;
			return wrongCount == 0;*/
		}
		
		// ReSharper disable once UnusedMember.Local
		internal static bool CompareCollection<T>(T[] a, T[] b)
		{
			if (a.Length != b.Length) return false;
			if (a.SequenceEqual(b)) return true;
			int i = 0;
			int indexOfMismatch = -1;
			while (i < a.Length)
			{
				if (!a[i].Equals(b[i]))
				{
					indexOfMismatch = i;
					break;
				}

				i++;
			}

			return indexOfMismatch == -1;
		}

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