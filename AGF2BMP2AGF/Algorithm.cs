﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AGF2BMP2AGF.Operation;

// ReSharper disable InconsistentNaming

namespace AGF2BMP2AGF
{
	internal class Algorithm
	{
		internal static ProcessData CurrentProcessData = new();
		internal static readonly Dictionary<int, FileStream> FileHandles = new();

		private const ulong AGF_TYPE_24BIT = 1;
		private const ulong AGF_TYPE_32BIT = 2;

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
				//CurrentProcessData.BmpFile.Data = buffer;
				//CurrentProcessData.BmpFile.PalLength = buffer.Length - (sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER));
				//if (!CurrentProcessData.Compare()) throw new InvalidOperationException("Output BMP read back did not match previous data.");

			}
			finally
			{
				fileStream.Close();
			}
		}

		internal static int Pack(int bmpFd, int outFd)
		{
			read_bmp(bmpFd, out var decodedData, out _, out var bmi);
			EncodeColorMap(decodedData, bmi, out var encodedData, out var outAlphaBuff, out var palBuff);
			CurrentProcessData.Encoding = new DecodingData(bmi, encodedData, palBuff, outAlphaBuff, decodedData);/*
			//if (!CurrentProcessData.CompareMapCoding(1)) throw new InvalidOperationException("Output BMP read back did not match previous data.");
			CurrentProcessData.OutBmpFile.AlphaBuff = outAlphaBuff.ToArray();
			CurrentProcessData.OutBmpFile.Buff = encodedData.ToArray();
			CurrentProcessData.OutBmpFile.PalLength = palBuff.Length;
			//if (!CurrentProcessData.Compare(1)) throw new InvalidOperationException("Output BMP read back did not match previous data.");*/
			write_agf(outFd, encodedData, palBuff, outAlphaBuff);
			return 0;
		}

		private static unsafe void write_agf(int fd, byte[] encodedData, RGBQUAD[] palArray, byte[] alphaBuff)
		{
			//if (!CurrentProcessData.CompareMapCoding(1)) throw new InvalidOperationException("Output BMP read back did not match previous data.");
			var fileStream = FileHandles[fd];
			try
			{
				fileStream.Write(StructToBytes(CurrentProcessData.AgfFile.AgfHeader), 0, sizeof(AGFHDR));
				var hdrBytes = Enumerable.Concat(StructToBytes(CurrentProcessData.AgfFile.Bmf), new byte[] { 0, 0 })
					.Concat(StructToBytes(CurrentProcessData.AgfFile.Bmi)).Concat(palArray.SelectMany(StructToBytes)).ToArray();
				write_sect(fd, hdrBytes);
				write_sect(fd, encodedData);
				fileStream.Write(StructToBytes(CurrentProcessData.AgfFile.AcifHeader), 0, sizeof(ACIFHDR));
				write_sect(fd, alphaBuff);
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
			read_sect(fd, out var bmphdr_buff, out var bmphdr_len);
			read_sect(fd, out var buff, out var len);
			//CurrentProcessData.AgfFile.Data = buff.ToArray();
			// Notice there's a gap of 2 bytes between these... alignment I guess.
			var bmf = ByteArrayToStructure<BITMAPFILEHEADER>(bmphdr_buff);
			var bmi = ByteArrayToStructure<BITMAPINFOHEADER>(bmphdr_buff, 16);
			CurrentProcessData.AgfFile.Bmf = bmf;
			CurrentProcessData.AgfFile.Bmi = bmi;
			int offset = 16 + sizeof(BITMAPINFOHEADER);
			RGBQUAD[] pal = ByteArrayToStructureArray<RGBQUAD>(bmphdr_buff, offset, (int)bmphdr_len - offset);
			//CurrentProcessData.AgfFile.PalLength = palLength;
			if (hdr.type == AGF_TYPE_32BIT)
			{
				read(fd, out ACIFHDR acifhdr, sizeof(ACIFHDR));
				CurrentProcessData.AgfFile.AcifHeader = acifhdr;
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
						4);
				}
			}
			else
			{
				if (out_filename != null)
				{
					write_bmp_ex(out_filename,
						buff,
						len,
						bmi.biWidth,
						bmi.biHeight,
						bmi.biBitCount / 8,
						bmi.biClrUsed,
						pal);
				}
			}

			close(fd);
			return 0;
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

		private static void EncodeColorMap(byte[] decodedData, BITMAPINFOHEADER bmi, out byte[] encodedData, out byte[] alpha_buff, out RGBQUAD[] pal)
		{
			uint rgb_stride = (uint)((CurrentProcessData.Decoding.Bmi.biWidth * CurrentProcessData.Decoding.Bmi.biBitCount / 8 + 3) & ~3); //(uint)((bmi.biWidth * bmi.biBitCount / 8 + 3) & ~3);
			alpha_buff = new byte[CurrentProcessData.Decoding.AlphaBuff.Length];//byte[rgb_stride * bmi.biHeight / 4]; //bmi.biWidth * bmi.biHeight * bmi.biBitCount/8];
			encodedData = new byte[CurrentProcessData.Decoding.EncodedData.Length];
			bool useExistingPal = CurrentProcessData?.Decoding?.PalArray != null;
			pal = useExistingPal ? CurrentProcessData.Decoding.PalArray : new RGBQUAD[256/*CurrentProcessData.AgfFile.PalLength*/];
			var palList = useExistingPal ? pal.ToList() : new List<RGBQUAD>();
			CurrentProcessData.Decoding.AdditionalPalMap = new Dictionary<RGBQUAD, int>();
			for (long y = 0; y < bmi.biHeight; y++)
			{
				uint alp_lineLoc = (uint)((bmi.biHeight - y - 1) * bmi.biWidth);
				uint rgba_line = (uint)(y * bmi.biWidth * 4);
				long rgb_line = y * rgb_stride;
				for (long x = 0; x < bmi.biWidth; x++)
				{

					long blueIndex = rgba_line + x * 4 + 0;
					if (CurrentProcessData.Decoding.Bmi.biBitCount == 8)
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
							palIndex = FindNearestPal(newPal, palList, CurrentProcessData.Decoding.AdditionalPalMap);/*
							palIndex = palList.Count;
							palList.Add(newPal);*/
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

		// ReSharper disable UnusedParameter.Local
		private static void write_bmp_ex(string outFilename, byte[] buff, ulong len, int bmiBiWidth, int bmiBiHeight, int bmiBiBitCount, uint bmiBiClrUsed, RGBQUAD[] pal)
		// ReSharper restore UnusedParameter.Local
		{
			throw new NotImplementedException();
		}

		private static unsafe void write_bmp(string filename,
			byte[] buff,
			int width,
			int height,
			byte depth_bytes)
		{
			//var f = Array.FindIndex(buff, a => a != 0);
			BITMAPFILEHEADER bmf = new BITMAPFILEHEADER();
			BITMAPINFOHEADER bmi = new BITMAPINFOHEADER();

			bmf.bfType = 0x4D42;
			bmf.bfSize = (uint)sizeof(BITMAPFILEHEADER) + (uint)sizeof(BITMAPINFOHEADER) + (uint)buff.Length;
			bmf.bfOffBits = (uint)sizeof(BITMAPFILEHEADER) + (uint)sizeof(BITMAPINFOHEADER);

			bmi.biSize = (uint)sizeof(BITMAPINFOHEADER);
			bmi.biWidth = width;
			bmi.biHeight = height;
			bmi.biPlanes = 1;
			bmi.biBitCount = (ushort)(depth_bytes * 8);
			Algorithm.CurrentProcessData.OutBmpFile.FileName = filename;
			Algorithm.CurrentProcessData.OutBmpFile.Bmf = bmf;
			Algorithm.CurrentProcessData.OutBmpFile.Bmi = bmi;
			//CurrentProcessData.OutBmpFile.Data = buff.ToArray();
			var fileStream = File.OpenWrite(filename);
			fileStream.Write(Operation.StructToBytes(bmf), 0, sizeof(BITMAPFILEHEADER));
			fileStream.Write(Operation.StructToBytes(bmi), 0, sizeof(BITMAPINFOHEADER));
			fileStream.Write(buff, 0, buff.Length);
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
