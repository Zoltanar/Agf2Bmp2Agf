using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AGF2BMP2AGF.Operation;

// ReSharper disable InconsistentNaming

namespace AGF2BMP2AGF
{
	internal static class Algorithm
	{
		private const ulong AGF_TYPE_24BIT = 1;
		private const ulong AGF_TYPE_32BIT = 2;
		private static bool useExistingPal = true;

		internal static ProcessData CurrentProcessData = new();
		internal static readonly Dictionary<int, FileStream> FileHandles = new();

		private static unsafe void read_bmp(int fd, out byte[] buffer, out long length, out BITMAPINFOHEADER bmi)
		{
			var fileStream = FileHandles[fd];
			CurrentProcessData.BmpFile.FileName = fileStream.Name;
			try
			{
				var bmfB = new byte[sizeof(BITMAPFILEHEADER)];
				var bmiB = new byte[sizeof(BITMAPINFOHEADER)];
				length = fileStream.Length - (bmfB.Length + bmiB.Length);
				buffer = new byte[length];
				fileStream.Read(bmfB, 0, sizeof(BITMAPFILEHEADER));
				fileStream.Read(bmiB, 0, sizeof(BITMAPINFOHEADER));
				fileStream.Read(buffer, 0, (int)length);
				// ReSharper disable once UnusedVariable
				var bmf = ByteArrayToStructure<BITMAPFILEHEADER>(bmfB);
				bmi = ByteArrayToStructure<BITMAPINFOHEADER>(bmiB);
			}
			finally
			{
				fileStream.Close();
			}
		}

		internal static int Pack(int bmpFd, int outFd)
		{
			read_bmp(bmpFd, out var decodedData, out _, out var bmi);
			byte[] encodedData;
			byte[] outAlphaBuff = null;
			RGBQUAD[] palBuff;
			if (CurrentProcessData.AgfFile.AgfHeader.type == AGF_TYPE_32BIT) EncodeColorMap(decodedData, bmi, out encodedData, out outAlphaBuff, out palBuff);
			else
			{
				palBuff = ByteArrayToStructureArray<RGBQUAD>(decodedData,0,1024);
				EncodeColorMap24bpp(decodedData.Skip(1024).ToArray(), bmi, palBuff, out encodedData);
			}
			CurrentProcessData.Encoding = new DecodingData(bmi, encodedData, palBuff, outAlphaBuff, decodedData);
			write_agf(outFd, encodedData, palBuff, outAlphaBuff);
			return 0;
		}


		private static unsafe void write_agf(int fd, byte[] encodedData, RGBQUAD[] palArray, byte[] alphaBuff)
		{
			var fileStream = FileHandles[fd];
			try
			{
				fileStream.Write(StructToBytes(CurrentProcessData.AgfFile.AgfHeader), 0, sizeof(AGFHDR));
				var hdrBytes = StructToBytes(CurrentProcessData.AgfFile.Bmf).Concat(new byte[] { 0, 0 })
					.Concat(StructToBytes(CurrentProcessData.AgfFile.Bmi)).Concat(palArray.SelectMany(StructToBytes)).ToArray();
				write_sect(fd, hdrBytes);
				write_sect(fd, encodedData);
				if (alphaBuff != null)
				{
					fileStream.Write(StructToBytes(CurrentProcessData.AgfFile.AcifHeader), 0, sizeof(ACIFHDR));
					write_sect(fd, alphaBuff);
				}
			}
			finally
			{
				fileStream.Dispose();
			}
		}

		internal static unsafe int Unpack(int fd, string filename, string out_filename)
		{
			read(fd, out AGFHDR hdr, sizeof(AGFHDR));
			CurrentProcessData.AgfFile.FileName = filename;
			CurrentProcessData.AgfFile.AgfHeader = hdr;
			if (hdr.type != AGF_TYPE_24BIT && hdr.type != AGF_TYPE_32BIT)
			{
				Program.Print(Program.ErrorColor, $"File {filename} was of unsupported type (possibly an MPEG): {hdr.type}");
				return -1;
			}
			read_sect(fd, out var bmpHeaderBuff, out var bmpHeaderLen);
			read_sect(fd, out var buff, out _);
			//CurrentProcessData.AgfFile.Data = buff.ToArray();
			// Notice there's a gap of 2 bytes between these... alignment I guess.
			var bmf = ByteArrayToStructure<BITMAPFILEHEADER>(bmpHeaderBuff);
			var bmi = ByteArrayToStructure<BITMAPINFOHEADER>(bmpHeaderBuff, 16);
			CurrentProcessData.AgfFile.Bmf = bmf;
			CurrentProcessData.AgfFile.Bmi = bmi;
			int offset = 16 + sizeof(BITMAPINFOHEADER);
			RGBQUAD[] pal = ByteArrayToStructureArray<RGBQUAD>(bmpHeaderBuff, offset, (int)bmpHeaderLen - offset);
			//CurrentProcessData.AgfFile.PalLength = palLength;
			if (hdr.type == AGF_TYPE_32BIT)
			{
				read(fd, out ACIFHDR acifHeader, sizeof(ACIFHDR));
				CurrentProcessData.AgfFile.AcifHeader = acifHeader;
				read_sect(fd, out var alpha_buff, out _);
				//CurrentProcessData.AgfFile.AlphaBuff = alpha_buff.ToArray();
				var colorMap = DecodeColorMap(bmi, buff, pal, alpha_buff);
				if (out_filename != null)
				{
					write_bmp(out_filename,
						colorMap, //rgba_buff,
											//rgba_len,
						bmi.biWidth,
						bmi.biHeight,
						4,
						null);
				}
			}
			else
			{
				if (out_filename != null)
				{
					var colorMap = DecodeColorMap24Bit(bmi, buff, pal);
					write_bmp(out_filename,
						colorMap,
						bmi.biWidth,
						bmi.biHeight,
						1,
						pal);
				}
			}
			close(fd);
			return 0;
		}

		// ReSharper disable once UnusedMember.Global
		internal static unsafe void ByteCompare(string file1, string file2)
		{
			var oBytes = File.ReadAllBytes(file1);
			var nBytes = File.ReadAllBytes(file2);
			var equal = ProcessData.CompareCollection(oBytes, nBytes);
			if (!equal)
			{
					var oBmf = ByteArrayToStructure<BITMAPFILEHEADER>(oBytes);
					var nBmf = ByteArrayToStructure<BITMAPFILEHEADER>(nBytes);
					// ReSharper disable UnusedVariable
					var equalBmf = oBmf.Equals(nBmf);
					var oBmi = ByteArrayToStructure<BITMAPINFOHEADER>(oBytes, sizeof(BITMAPINFOHEADER));
					var nBmi = ByteArrayToStructure<BITMAPINFOHEADER>(nBytes, sizeof(BITMAPINFOHEADER));
					var equalBmi = oBmi.Equals(nBmi);
					// ReSharper restore UnusedVariable
			}
		}

		private static byte[] DecodeColorMap(BITMAPINFOHEADER bmi, byte[] encodedData, RGBQUAD[] palArray, byte[] alphaBuff)
		{
			ulong rgba_len = (ulong)(bmi.biWidth * bmi.biHeight * 4);
			byte[] decodedData = new byte[rgba_len];
			uint rgb_stride = (uint)((bmi.biWidth * bmi.biBitCount / 8 + 3) & ~3);
			for (long y = 0; y < bmi.biHeight; y++)
			{
				uint alp_lineLoc = (uint)((bmi.biHeight - y - 1) * bmi.biWidth);
				uint rgba_line = (uint)(y * bmi.biWidth * 4);
				long rgb_line = y * rgb_stride;
				for (long x = 0; x < bmi.biWidth; x++)
				{
					long blueIndex = rgba_line + x * 4 + 0;
					if (bmi.biBitCount == 8)
					{
						var palIndex = encodedData[y * rgb_stride + x];
						decodedData[blueIndex] = palArray[palIndex].rgbBlue;
						decodedData[blueIndex + 1] = palArray[palIndex].rgbGreen;
						decodedData[blueIndex + 2] = palArray[palIndex].rgbRed;
					}
					else
					{
						decodedData[blueIndex] = encodedData[rgb_line + x * 3 + 0];
						decodedData[blueIndex + 1] = encodedData[rgb_line + x * 3 + 1];
						decodedData[blueIndex + 2] = encodedData[rgb_line + x * 3 + 2];
					}
					decodedData[blueIndex + 3] = alphaBuff[alp_lineLoc + x];
				}
			}
			CurrentProcessData.Decoding = new DecodingData(bmi, encodedData, palArray, alphaBuff, decodedData);
			return decodedData;
		}
		
		private static byte[] DecodeColorMap24Bit(BITMAPINFOHEADER bmi, byte[] encodedData, RGBQUAD[] palArray)
		{
			ulong rgba_len = (ulong)(bmi.biWidth * bmi.biHeight);
			byte[] decodedData = new byte[rgba_len];
			uint rgb_stride = (uint)((bmi.biWidth * bmi.biBitCount / 8 + 3) & ~3);
			for (long y = 0; y < bmi.biHeight; y++)
			{
				uint rgba_line = (uint)(y * bmi.biWidth);
				long rgb_line = y * rgb_stride;
				for (long x = 0; x < bmi.biWidth; x++)
				{
					long blueIndex = rgba_line + x;
					if (bmi.biBitCount == 8)
					{
						var palIndex = encodedData[y * rgb_stride + x];
						decodedData[blueIndex] = palIndex;
					}
					else
					{
						decodedData[blueIndex] = encodedData[rgb_line + x * 3 + 0];
						decodedData[blueIndex + 1] = encodedData[rgb_line + x * 3 + 1];
						decodedData[blueIndex + 2] = encodedData[rgb_line + x * 3 + 2];
					}
				}
			}
			CurrentProcessData.Decoding = new DecodingData(bmi, encodedData, palArray, null, decodedData);
			return decodedData;
		}

		private static void EncodeColorMap24bpp(byte[] decodedData, BITMAPINFOHEADER bmi, RGBQUAD[] palBuff, out byte[] encodedData)
		{
			ulong rgba_len = (ulong)(bmi.biWidth * bmi.biHeight);
			encodedData = new byte[rgba_len];
			uint rgb_stride = (uint)((bmi.biWidth * bmi.biBitCount / 8 + 3) & ~3);
			for (long y = 0; y < bmi.biHeight; y++)
			{
				uint rgba_line = (uint)(y * bmi.biWidth);
				long rgb_line = y * rgb_stride;
				for (long x = 0; x < bmi.biWidth; x++)
				{
					long blueIndex = rgba_line + x;
					if (bmi.biBitCount == 8)
					{
						var palIndex = decodedData[blueIndex];
						encodedData[y * rgb_stride + x] = palIndex;
					}
					else
					{
						encodedData[rgb_line + x * 3 + 0] = decodedData[blueIndex];
						encodedData[rgb_line + x * 3 + 1] = decodedData[blueIndex + 1];
						encodedData[rgb_line + x * 3 + 2] = decodedData[blueIndex + 2];
					}
				}
			}
			CurrentProcessData.Encoding = new DecodingData(bmi, encodedData, palBuff, null, decodedData);
		}

		private static void EncodeColorMap(byte[] decodedData, BITMAPINFOHEADER bmi, out byte[] encodedData, out byte[] alpha_buff, out RGBQUAD[] pal)
		{
			var original8bpp = CurrentProcessData.Decoding.Bmi.biBitCount == 8;
			if (!original8bpp) { }
			uint rgb_stride = (uint)((bmi.biWidth * CurrentProcessData.Decoding.Bmi.biBitCount / 8 + 3) & ~3);
			alpha_buff = new byte[original8bpp ? bmi.biHeight * bmi.biWidth : CurrentProcessData.Decoding.AlphaBuff.Length];
			encodedData = new byte[original8bpp ? bmi.biHeight * bmi.biWidth : CurrentProcessData.Decoding.EncodedData.Length];
			var palList = useExistingPal ? CurrentProcessData?.Decoding?.PalArray.ToList() : new List<RGBQUAD>();
			var additionalPalMap = new Dictionary<RGBQUAD, int>();
			for (long y = 0; y < bmi.biHeight; y++)
			{
				uint alp_lineLoc = (uint)((bmi.biHeight - y - 1) * bmi.biWidth);
				uint rgba_line = (uint)(y * bmi.biWidth * 4);
				long rgb_line = y * rgb_stride;
				for (long x = 0; x < bmi.biWidth; x++)
				{
					long blueIndex = rgba_line + x * 4 + 0;
					if (original8bpp)
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
						encodedData[y * rgb_stride + x] = (byte)palIndex;
					}
					else
					{
						encodedData[rgb_line + x * 3] = decodedData[blueIndex];
						encodedData[rgb_line + x * 3 + 1] = decodedData[blueIndex + 1];
						encodedData[rgb_line + x * 3 + 2] = decodedData[blueIndex + 2];
					}
					alpha_buff[alp_lineLoc + x] = decodedData[blueIndex + 3];
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

		private static void close(int fileHandle)
		{
			var file = FileHandles[fileHandle];
			file.Close();
			file.Dispose();
			FileHandles.Remove(fileHandle);
		}

		internal static int OpenFileOrDie(string filename, FileMode fileMode)
		{
			if (!File.Exists(filename) && fileMode != FileMode.Create && fileMode != FileMode.CreateNew) throw new FileNotFoundException("File to convert was not found", filename);
			var fileStream = File.Open(filename, fileMode);
			var fileHandle = FileHandles.Count;
			FileHandles[fileHandle] = fileStream;
			return fileHandle;
		}

		private static void read<T>(int fileHandle, out T structure, int size) where T : struct
		{
			var bytes = new byte[size];
			FileHandles[fileHandle].Read(bytes, 0, size);
			structure = Operation.ByteArrayToStructure<T>(bytes);
		}

		private static byte[] read(int fileHandle, int size)
		{
			var bytes = new byte[size];
			FileHandles[fileHandle].Read(bytes, 0, size);
			return bytes;
		}

		private static unsafe void write_bmp(string filename,
			byte[] buff,
			int width,
			int height,
			byte depth_bytes,
			RGBQUAD[] palArray)
		{
			BITMAPFILEHEADER bmf = new BITMAPFILEHEADER();
			BITMAPINFOHEADER bmi = new BITMAPINFOHEADER();

			bmf.bfType = 0x4D42;
			var offBits = (uint)(sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER) + (sizeof(RGBQUAD) * (palArray?.Length ?? 0)));
			bmf.bfSize = offBits + (uint)buff.Length;
			bmf.bfOffBits = offBits;

			bmi.biSize = (uint)sizeof(BITMAPINFOHEADER);
			bmi.biWidth = width;
			bmi.biHeight = height;
			bmi.biPlanes = 1;
			bmi.biBitCount = (ushort)(depth_bytes * 8);
			CurrentProcessData.OutBmpFile.FileName = filename;
			CurrentProcessData.OutBmpFile.Bmf = bmf;
			CurrentProcessData.OutBmpFile.Bmi = bmi;
			//CurrentProcessData.OutBmpFile.Data = buff.ToArray();
			var fileStream = File.OpenWrite(filename);
			fileStream.Write(Operation.StructToBytes(bmf), 0, sizeof(BITMAPFILEHEADER));
			fileStream.Write(Operation.StructToBytes(bmi), 0, sizeof(BITMAPINFOHEADER));
			var paletteAndMap = depth_bytes == 1 
				? (palArray ?? throw new ArgumentNullException(nameof(palArray),"A palette must exist for 8Bpp image."))
						.SelectMany(StructToBytes).Concat(buff).ToArray() 
				: buff;
			fileStream.Write(paletteAndMap, 0, paletteAndMap.Length);
			fileStream.Close();
		}
		private static unsafe void read_sect(int fd, out byte[] out_buff, out ulong out_len)
		{
			read(fd, out AGFSECTHDR hdr, sizeof(AGFSECTHDR));
			var len = hdr.length;
			var buff = read(fd, (int)len);

			out_len = hdr.original_length;
			out_buff = len == out_len ? buff : UnpackLZSS(buff, len, out_len);
		}

		// ReSharper disable once UnusedParameter.Local
		private static unsafe void write_sect(int fileHandle, byte[] data, int? setOutLen = null)
		{
			AGFSECTHDR sectHdr;
			byte[] out_buff = data;
			var len = data.Length;
			var out_len = len;
			/*if (setOutLen.HasValue)
			{
				out_len = setOutLen.Value;
				out_buff = PackLZSS(data, (uint)data.Length, ref out_len);
			}*/
			sectHdr.length = (uint)out_len;//(uint)data.Length;
			sectHdr.original_length = (uint)data.Length;
			sectHdr.original_length2 = (uint)data.Length;
			FileHandles[fileHandle].Write(Operation.StructToBytes(sectHdr), 0, sizeof(AGFSECTHDR));
			FileHandles[fileHandle].Write(out_buff, 0, out_len);
		}

		private static byte[] UnpackLZSS(byte[] buff, ulong len, ulong outLen)
		{
			return Compression.UnpackLZSS(buff, len, outLen);
		}

		private static byte[] PackLZSS(byte[] buff, ulong len, ref int outLen)
		{
			return Compression.PackLZSS(buff, len, ref outLen);
		}
	}

}
