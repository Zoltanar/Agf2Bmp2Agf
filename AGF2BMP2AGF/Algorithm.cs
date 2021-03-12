﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static AGF2BMP2AGF.Operation;

// ReSharper disable InconsistentNaming

namespace AGF2BMP2AGF
{
	public static class Algorithm
	{
		private const ulong AGF_TYPE_24BIT = 1;
		private const ulong AGF_TYPE_32BIT = 2;
		private static bool useExistingPal = true;

		internal static ProcessData CurrentProcessData = new();
		private static readonly Dictionary<int, FileStream> FileHandles = new();

		private static void read_bmp(int fd, out byte[] buffer, out long length, out BITMAPINFOHEADER bmi)
		{
			var fileStream = FileHandles[fd];
			CurrentProcessData.BmpFile.FileName = fileStream.Name;
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
			if (CurrentProcessData.AgfFile.AgfHeader.type == AGF_TYPE_32BIT) EncodeColorMapWithAlpha(decodedData, bmi, out encodedData, out outAlphaBuff, out palBuff);
			else
			{
				palBuff = bmi.biBitCount == 8 ? ByteArrayToStructureArray<RGBQUAD>(decodedData, 0, 1024) : null;
				var decodedColorMap = bmi.biBitCount == 8 ? decodedData.Skip(1024).ToArray() : decodedData;
				EncodeColorMapNoAlpha(decodedColorMap, bmi, palBuff, out encodedData);
			}
			CurrentProcessData.Encoding = new DecodingData(bmi, encodedData, palBuff, outAlphaBuff, decodedData);
			write_agf(outFd, encodedData, palBuff, outAlphaBuff);
			return 0;
		}


		private static void write_agf(int fd, byte[] encodedData, RGBQUAD[] palArray, byte[] alphaBuff)
		{
			var fileStream = FileHandles[fd];
			try
			{
				fileStream.Write(StructToBytes(CurrentProcessData.AgfFile.AgfHeader), 0, Marshal.SizeOf<AGFHDR>());
				var hdrBytes = StructToBytes(CurrentProcessData.AgfFile.Bmf).Concat(new byte[] { 0, 0 })
					.Concat(StructToBytes(CurrentProcessData.AgfFile.Bmi));
				if (palArray != null) hdrBytes = hdrBytes.Concat(palArray.SelectMany(StructToBytes));

				write_sect(fd, hdrBytes.ToArray());
				write_sect(fd, encodedData);
				if (alphaBuff != null)
				{
					fileStream.Write(StructToBytes(CurrentProcessData.AgfFile.AcifHeader), 0, Marshal.SizeOf<ACIFHDR>());
					write_sect(fd, alphaBuff);
				}
			}
			finally
			{
				fileStream.Dispose();
			}
		}

		public static int Unpack(int fd, string filename, string out_filename)
		{
			read(fd, out AGFHDR hdr, Marshal.SizeOf<AGFHDR>());
			CurrentProcessData.AgfFile.FileName = filename;
			CurrentProcessData.AgfFile.AgfHeader = hdr;
			if (hdr.type != AGF_TYPE_24BIT && hdr.type != AGF_TYPE_32BIT)
			{
				Program.Print(Program.ErrorColor, $"File {filename} was of unsupported type (possibly an MPEG): {hdr.type}");
				return -1;
			}
			read_sect(fd, out var bmpHeaderBuff, out var bmpHeaderLen);
			read_sect(fd, out var buff, out _);
			// Notice there's a gap of 2 bytes between these... alignment I guess.
			var bmf = ByteArrayToStructure<BITMAPFILEHEADER>(bmpHeaderBuff);
			var bmi = ByteArrayToStructure<BITMAPINFOHEADER>(bmpHeaderBuff, 16);
			CurrentProcessData.AgfFile.Bmf = bmf;
			CurrentProcessData.AgfFile.Bmi = bmi;
			int offset = 16 + Marshal.SizeOf<BITMAPINFOHEADER>();
			RGBQUAD[] pal = ByteArrayToStructureArray<RGBQUAD>(bmpHeaderBuff, offset, (int)bmpHeaderLen - offset);
			if (hdr.type == AGF_TYPE_32BIT)
			{
				read(fd, out ACIFHDR acifHeader, Marshal.SizeOf<ACIFHDR>());
				CurrentProcessData.AgfFile.AcifHeader = acifHeader;
				read_sect(fd, out var alpha_buff, out _);
				var colorMap = DecodeColorMapWithAlpha(bmi, buff, pal, alpha_buff);
				if (out_filename != null)
				{
					write_bmp(out_filename,
						colorMap, //rgba_buff,
											//rgba_len,
						bmi.biWidth,
						bmi.biHeight,
						4,
						null,
						true);
				}
			}
			else
			{
				if (out_filename != null)
				{
					CurrentProcessData.Decoding = new DecodingData(bmi, buff, null, null, buff);
					write_bmp(out_filename,
						buff,
						bmi.biWidth,
						bmi.biHeight,
						bmi.biBitCount / 8,
						pal,
						bmf.bfOffBits == 54);
				}
			}
			close(fd);
			return 0;
		}

		private static byte[] DecodeColorMapWithAlpha(BITMAPINFOHEADER bmi, byte[] encodedData, RGBQUAD[] palArray, byte[] alphaBuff)
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
		
		private static void EncodeColorMapNoAlpha(byte[] decodedData, BITMAPINFOHEADER bmi, RGBQUAD[] palBuff, out byte[] encodedData)
		{/*
			var bytesPerPixel = bmi.biBitCount / 8;
			//the stride must be padded to 4 bytes
			uint rgb_stride = (uint)((bmi.biWidth * bytesPerPixel + 3) & ~3);
			encodedData = new byte[bmi.biHeight * rgb_stride];
			for (long y = 0; y < bmi.biHeight; y++)
			{
				for (long x = 0; x < bmi.biWidth; x++)
				{
					long blueIndex = y * rgb_stride + x;
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
			}*/
			encodedData = decodedData.ToArray();
			CurrentProcessData.Encoding = new DecodingData(bmi, encodedData, palBuff, null, decodedData);
		}

		private static void EncodeColorMapWithAlpha(byte[] decodedData, BITMAPINFOHEADER bmi, out byte[] encodedData, out byte[] alpha_buff, out RGBQUAD[] pal)
		{
			var original8bpp = CurrentProcessData.Decoding.Bmi.biBitCount == 8;
			if (!original8bpp) { }
			uint rgb_stride = (uint)((bmi.biWidth * CurrentProcessData.Decoding.Bmi.biBitCount / 8 + 3) & ~3);
			alpha_buff = new byte[original8bpp ? bmi.biHeight * bmi.biWidth : CurrentProcessData.Decoding.AlphaBuff.Length];
			encodedData = new byte[original8bpp ? bmi.biHeight * rgb_stride : CurrentProcessData.Decoding.EncodedData.Length];
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

		public static int OpenFileOrDie(string filename, FileMode fileMode)
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

		private static void write_bmp(string filename,
			byte[] buff,
			int width,
			int height,
			int depth_bytes,
			RGBQUAD[] palArray,
			bool skipPalette)
		{
			var bmf = new BITMAPFILEHEADER();
			var bmi = new BITMAPINFOHEADER();
			bmf.bfType = 0x4D42;
			var paletteSize = skipPalette ? 0 : (Marshal.SizeOf<RGBQUAD>() * (palArray?.Length ?? 0));
			var offBits = Marshal.SizeOf<BITMAPFILEHEADER>() + Marshal.SizeOf<BITMAPINFOHEADER>() + paletteSize;
			bmf.bfSize = (uint) (offBits + buff.Length);
			bmf.bfOffBits = (uint) offBits;

			bmi.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
			bmi.biWidth = width;
			bmi.biHeight = height;
			bmi.biPlanes = 1;
			bmi.biBitCount = (ushort)(depth_bytes * 8);
			CurrentProcessData.OutBmpFile.FileName = filename;
			CurrentProcessData.OutBmpFile.Bmf = bmf;
			CurrentProcessData.OutBmpFile.Bmi = bmi;
			//CurrentProcessData.OutBmpFile.Data = buff.ToArray();
			var fileStream = File.OpenWrite(filename);
			fileStream.Write(Operation.StructToBytes(bmf), 0, Marshal.SizeOf<BITMAPFILEHEADER>());
			fileStream.Write(Operation.StructToBytes(bmi), 0, Marshal.SizeOf<BITMAPINFOHEADER>());
			var paletteAndMap = depth_bytes == 1 && !skipPalette
				? (palArray ?? throw new ArgumentNullException(nameof(palArray), "A palette must exist for 8Bpp image."))
						.SelectMany(StructToBytes).Concat(buff).ToArray()
				: buff;
			fileStream.Write(paletteAndMap, 0, paletteAndMap.Length);
			fileStream.Close();
		}
		private static void read_sect(int fd, out byte[] out_buff, out ulong out_len)
		{
			read(fd, out AGFSECTHDR hdr, Marshal.SizeOf<AGFSECTHDR>());
			var len = hdr.length;
			var buff = read(fd, (int)len);

			out_len = hdr.original_length;
			out_buff = len == out_len ? buff : UnpackLZSS(buff, (int) out_len);
		}

		// ReSharper disable once UnusedParameter.Local
		private static void write_sect(int fileHandle, byte[] data, int? setOutLen = null)
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
			FileHandles[fileHandle].Write(Operation.StructToBytes(sectHdr), 0, Marshal.SizeOf<AGFSECTHDR>());
			FileHandles[fileHandle].Write(out_buff, 0, out_len);
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
