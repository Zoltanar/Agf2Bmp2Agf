using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static AGF2BMP2AGF.Operation;

namespace AGF2BMP2AGF
{
	public static class Algorithm
	{
		private const ulong AgfType24Bit = 1;
		private const ulong AgfType32Bit = 2;
		private static bool useExistingPal = true;

		private static void ReadBitmap(string fileName, out byte[] buffer, out long length, out BITMAPFILEHEADER bmf, out BITMAPINFOHEADER bmi)
		{
			var fileStream = OpenFileOrDie(fileName, FileMode.Open);
			try
			{
				var bmfB = new byte[Marshal.SizeOf<BITMAPFILEHEADER>()];
				var bmiB = new byte[Marshal.SizeOf<BITMAPINFOHEADER>()];
				length = fileStream.Length - (bmfB.Length + bmiB.Length);
				buffer = new byte[length];
				fileStream.Read(bmfB, 0, Marshal.SizeOf<BITMAPFILEHEADER>());
				fileStream.Read(bmiB, 0, Marshal.SizeOf<BITMAPINFOHEADER>());
				fileStream.Read(buffer, 0, (int)length);
				// ReSharper disable once UnusedVariable
				bmf = ByteArrayToStructure<BITMAPFILEHEADER>(bmfB);
				bmi = ByteArrayToStructure<BITMAPINFOHEADER>(bmiB);
			}
			finally
			{
				fileStream.Close();
			}
		}

		public static bool Pack(string inputFileName, string outputFileName, ProcessData processData)
		{
			ReadBitmap(inputFileName, out var decodedData, out _, out var bmf, out var bmi);
			byte[] encodedData;
			byte[] outAlphaBuff = null;
			RGBQUAD[] palBuff;
			if (processData.Decoding.AgfHeader.type == AgfType32Bit) EncodeColorMapWithAlpha(decodedData, bmi, processData, out encodedData, out outAlphaBuff, out palBuff);
			else
			{
				palBuff = bmi.biBitCount == 8 ? ByteArrayToStructureArray<RGBQUAD>(decodedData, 0, 1024) : null;
				var decodedColorMap = bmi.biBitCount == 8 ? decodedData.Skip(1024) : decodedData;
				encodedData = decodedColorMap.ToArray();
			}
			processData.Encoding = new DecodingData(bmf, bmi, encodedData, palBuff, outAlphaBuff, decodedData);
			WriteAgf(outputFileName, encodedData, palBuff, outAlphaBuff, processData);
			return true;
		}

		private static void WriteAgf(string fileName, byte[] encodedData, RGBQUAD[] palArray, byte[] alphaBuff, ProcessData processData)
		{
			var fileStream = OpenFileOrDie(fileName, FileMode.Create);
			try
			{
				fileStream.Write(StructToBytes(processData.Decoding.AgfHeader), 0, Marshal.SizeOf<AGFHDR>());
				var hdrBytes = StructToBytes(processData.Decoding.Bmf).Concat(new byte[] { 0, 0 })
					.Concat(StructToBytes(processData.Decoding.Bmi));
				if (palArray != null) hdrBytes = hdrBytes.Concat(palArray.SelectMany(StructToBytes));

				WriteSector(fileStream, hdrBytes.ToArray());
				WriteSector(fileStream, encodedData);
				if (alphaBuff == null) return;
				fileStream.Write(StructToBytes(processData.Decoding.AcifHeader), 0, Marshal.SizeOf<ACIFHDR>());
				WriteSector(fileStream, alphaBuff);
			}
			finally
			{
				fileStream.Dispose();
			}
		}

		public static bool Unpack(string inputFile, string outputFile, ProcessData processData)
		{
			var fileStream = OpenFileOrDie(inputFile, FileMode.Open);
			try
			{
				ReadToStructure(fileStream, out AGFHDR hdr, Marshal.SizeOf<AGFHDR>());
				if (hdr.type != AgfType24Bit && hdr.type != AgfType32Bit)
				{
					Program.Print(Program.ErrorColor, $"File {inputFile} was of unsupported type (possibly an MPEG): {hdr.type}");
					return false;
				}
				var bmpHeaderBuff = ReadSector(fileStream);
				var buff = ReadSector(fileStream);
				// Notice there's a gap of 2 bytes between these... alignment I guess.
				var bmf = ByteArrayToStructure<BITMAPFILEHEADER>(bmpHeaderBuff);
				var bmi = ByteArrayToStructure<BITMAPINFOHEADER>(bmpHeaderBuff, 16);
				int offset = 16 + Marshal.SizeOf<BITMAPINFOHEADER>();
				RGBQUAD[] pal = ByteArrayToStructureArray<RGBQUAD>(bmpHeaderBuff, offset, bmpHeaderBuff.Length - offset);
				if (hdr.type == AgfType32Bit)
				{
					ReadToStructure(fileStream, out ACIFHDR acifHeader, Marshal.SizeOf<ACIFHDR>());
					var alphaBuff = ReadSector(fileStream);
					var colorMap = DecodeColorMapWithAlpha(bmi, buff, pal, alphaBuff);
					processData.Decoding = new DecodingData(bmf, bmi, buff, pal, alphaBuff, colorMap, hdr, acifHeader);
					if (outputFile != null)
					{
						WriteBitmap(outputFile,
							colorMap,
							bmi.biWidth,
							bmi.biHeight,
							4,
							null,
							true);
					}
				}
				else
				{
					processData.Decoding = new DecodingData(bmf, bmi, buff, pal, null, buff, hdr, default);
					if (outputFile != null)
					{
						WriteBitmap(outputFile,
							buff,
							bmi.biWidth,
							bmi.biHeight,
							bmi.biBitCount / 8,
							pal,
							bmf.bfOffBits == 54);
					}
				}
			}
			finally
			{
				fileStream.Dispose();
			}
			return true;
		}

		private static byte[] DecodeColorMapWithAlpha(BITMAPINFOHEADER bmi, byte[] encodedData, RGBQUAD[] palArray, byte[] alphaBuff)
		{
			byte[] decodedData = new byte[bmi.biWidth * bmi.biHeight * 4];
			//must be padded to 4 bytes
			uint rgbStride = (uint)((bmi.biWidth * bmi.biBitCount / 8 + 3) & ~3);
			for (long y = 0; y < bmi.biHeight; y++)
			{
				uint alphaLineIndex = (uint)((bmi.biHeight - y - 1) * bmi.biWidth);
				uint rgbaLineIndex = (uint)(y * bmi.biWidth * 4);
				long rgbLineIndex = y * rgbStride;
				for (long x = 0; x < bmi.biWidth; x++)
				{
					long blueIndex = rgbaLineIndex + x * 4 + 0;
					if (bmi.biBitCount == 8)
					{
						var palIndex = encodedData[y * rgbStride + x];
						decodedData[blueIndex] = palArray[palIndex].rgbBlue;
						decodedData[blueIndex + 1] = palArray[palIndex].rgbGreen;
						decodedData[blueIndex + 2] = palArray[palIndex].rgbRed;
					}
					else
					{
						decodedData[blueIndex] = encodedData[rgbLineIndex + x * 3 + 0];
						decodedData[blueIndex + 1] = encodedData[rgbLineIndex + x * 3 + 1];
						decodedData[blueIndex + 2] = encodedData[rgbLineIndex + x * 3 + 2];
					}
					decodedData[blueIndex + 3] = alphaBuff[alphaLineIndex + x];
				}
			}
			return decodedData;
		}

		private static void EncodeColorMapWithAlpha(byte[] decodedData, BITMAPINFOHEADER bmi, ProcessData processData, out byte[] encodedData, out byte[] alphaData, out RGBQUAD[] pal)
		{
			//must be padded to 4 bytes
			uint rgbStride = (uint)((bmi.biWidth * processData.Decoding.Bmi.biBitCount / 8 + 3) & ~3);
			alphaData = new byte[processData.Decoding.Bmi.biBitCount == 8 ? bmi.biHeight * bmi.biWidth : processData.Decoding.AlphaBuff.Length];
			encodedData = new byte[processData.Decoding.Bmi.biBitCount == 8 ? bmi.biHeight * rgbStride : processData.Decoding.EncodedData.Length];
			var palList = useExistingPal ? processData.Decoding?.PalArray.ToList() : new List<RGBQUAD>();
			var additionalPalMap = new Dictionary<RGBQUAD, int>();
			for (long y = 0; y < bmi.biHeight; y++)
			{
				uint alphaLineIndex = (uint)((bmi.biHeight - y - 1) * bmi.biWidth);
				uint rgbaLineIndex = (uint)(y * bmi.biWidth * 4);
				long rgbLineIndex = y * rgbStride;
				for (long x = 0; x < bmi.biWidth; x++)
				{
					long blueIndex = rgbaLineIndex + x * 4 + 0;
					if (processData.Decoding.Bmi.biBitCount == 8)
					{
						var newPal = new RGBQUAD
						{
							rgbBlue = decodedData[blueIndex],
							rgbGreen = decodedData[blueIndex + 1],
							rgbRed = decodedData[blueIndex + 2]
						};
						var palIndex = palList.IndexOf(newPal);
						if (palIndex == -1)
						{
							palIndex = useExistingPal ? FindNearestPal(newPal, palList, additionalPalMap) : GetPal(newPal, palList, additionalPalMap);
						}
						encodedData[y * rgbStride + x] = (byte)palIndex;
					}
					else
					{
						encodedData[rgbLineIndex + x * 3] = decodedData[blueIndex];
						encodedData[rgbLineIndex + x * 3 + 1] = decodedData[blueIndex + 1];
						encodedData[rgbLineIndex + x * 3 + 2] = decodedData[blueIndex + 2];
					}
					alphaData[alphaLineIndex + x] = decodedData[blueIndex + 3];
				}
			}
			if (palList.Count < 256) palList.AddRange(Enumerable.Repeat(new RGBQUAD(), 256 - palList.Count));
			pal = palList.ToArray();
		}

		private static int GetPal(RGBQUAD inputPal, List<RGBQUAD> palList, Dictionary<RGBQUAD, int> additionalPalMap)
		{
			if (palList.Count < 256)
			{
				int palIndex = palList.Count;
				palList.Add(inputPal);
				return palIndex;
			}
			if (additionalPalMap.TryGetValue(inputPal, out var addPal)) return addPal;
			var ind = palList
				.IndexOf(palList
					.OrderBy(x =>
						Math.Sqrt(Math.Pow(x.rgbBlue - inputPal.rgbBlue, 2) + Math.Pow(x.rgbGreen - inputPal.rgbGreen, 2) + Math.Pow(x.rgbRed - inputPal.rgbRed, 2)))
					.First());
			additionalPalMap[inputPal] = ind;
			return ind;
		}

		private static int FindNearestPal(RGBQUAD inputPal, List<RGBQUAD> palList, Dictionary<RGBQUAD, int> additionalPalMap)
		{
			if (additionalPalMap.TryGetValue(inputPal, out var addPal)) return addPal;
			var ind = palList
				.IndexOf(palList
					.OrderBy(x =>
						Math.Sqrt(Math.Pow(x.rgbBlue - inputPal.rgbBlue, 2) + Math.Pow(x.rgbGreen - inputPal.rgbGreen, 2) + Math.Pow(x.rgbRed - inputPal.rgbRed, 2)))
					.First());
			additionalPalMap[inputPal] = ind;
			return ind;
		}

		public static FileStream OpenFileOrDie(string filename, FileMode fileMode)
		{
			Directory.CreateDirectory(Directory.GetParent(filename).FullName);
			var fileStream = File.Open(filename, fileMode);
			return fileStream;
		}

		public static void ReadToStructure<T>(Stream stream, out T structure, int size) where T : struct
		{
			var bytes = new byte[size];
			stream.Read(bytes, 0, size);
			structure = ByteArrayToStructure<T>(bytes);
		}

		private static void WriteBitmap(string fileName,
			byte[] buff,
			int width,
			int height,
			int depthBytes,
			RGBQUAD[] palArray,
			bool skipPalette)
		{
			var bmf = new BITMAPFILEHEADER();
			var bmi = new BITMAPINFOHEADER();
			bmf.bfType = 0x4D42;
			var paletteSize = skipPalette ? 0 : (Marshal.SizeOf<RGBQUAD>() * (palArray?.Length ?? 0));
			var offBits = Marshal.SizeOf<BITMAPFILEHEADER>() + Marshal.SizeOf<BITMAPINFOHEADER>() + paletteSize;
			bmf.bfSize = (uint)(offBits + buff.Length);
			bmf.bfOffBits = (uint)offBits;
			bmi.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
			bmi.biWidth = width;
			bmi.biHeight = height;
			bmi.biPlanes = 1;
			bmi.biBitCount = (ushort)(depthBytes * 8);
			var fileStream = OpenFileOrDie(fileName, FileMode.Create);
			fileStream.Write(StructToBytes(bmf), 0, Marshal.SizeOf<BITMAPFILEHEADER>());
			fileStream.Write(StructToBytes(bmi), 0, Marshal.SizeOf<BITMAPINFOHEADER>());
			var paletteAndMap = depthBytes == 1 && !skipPalette
				? (palArray ?? throw new ArgumentNullException(nameof(palArray), "A palette must exist for 8Bpp image."))
						.SelectMany(StructToBytes).Concat(buff).ToArray()
				: buff;
			fileStream.Write(paletteAndMap, 0, paletteAndMap.Length);
			fileStream.Close();
		}

		private static byte[] ReadSector(Stream stream)
		{
			ReadToStructure(stream, out AGFSECTHDR hdr, Marshal.SizeOf<AGFSECTHDR>());
			var len = hdr.length;
			var buff = new byte[len];
			stream.Read(buff, 0, (int)len);
			return hdr.original_length == hdr.length ? buff : UnpackLZSS(buff, (int)hdr.original_length);
		}

		// ReSharper disable once UnusedParameter.Local
		private static void WriteSector(Stream stream, byte[] data, int? setOutLen = null)
		{
			AGFSECTHDR sectHdr;
			byte[] writeData = data;
			var writeLength = data.Length;
			/*if (setOutLen.HasValue)
			{
				out_len = setOutLen.Value;
				out_buff = PackLZSS(data, (uint)data.Length, ref out_len);
			}*/
			sectHdr.length = (uint)writeLength;
			sectHdr.original_length = (uint)data.Length;
			sectHdr.original_length2 = (uint)data.Length;
			stream.Write(StructToBytes(sectHdr), 0, Marshal.SizeOf<AGFSECTHDR>());
			stream.Write(writeData, 0, writeLength);
		}

		private static byte[] UnpackLZSS(byte[] inputData, int outLen)
		{
			return Compression.UnpackLZSS(inputData, outLen);
		}

		// ReSharper disable once UnusedMember.Local
		private static byte[] PackLZSS(byte[] inputData, int outLen)
		{
			return Compression.PackLZSS(inputData, outLen);
		}
	}

}
