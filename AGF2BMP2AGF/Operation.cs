﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace AGF2BMP2AGF
{
	public static class Operation
	{
		public static byte[] StructToBytes<T>(T str) where T : struct
		{
			int size = Marshal.SizeOf(str);
			byte[] arr = new byte[size];

			IntPtr ptr = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(str, ptr, true);
			Marshal.Copy(ptr, arr, 0, size);
			Marshal.FreeHGlobal(ptr);
			return arr;
		}

		public static unsafe T ByteArrayToStructure<T>(byte[] bytes, int offset = 0) where T : struct
		{
			return ByteArrayToStructure<T>(bytes, offset, out _);
		}

		private static unsafe T ByteArrayToStructure<T>(byte[] bytes, int offset, out byte* initPtr) where T : struct
		{
			fixed (byte* ptr = &bytes[offset])
			{
				initPtr = ptr;
				return (T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T));
			}
		}

		public static unsafe T[] ByteArrayToStructureArray<T>(byte[] bytes, int offset, int length) where T : struct
		{
			int traveled = 0;
			var list = new List<T>();
			while (traveled < length)
			{
				fixed (byte* ptr = &bytes[offset + traveled])
				{
					list.Add((T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T)));
				}
				traveled += Marshal.SizeOf<T>();
			}
			return list.ToArray();
		}

		// ReSharper disable once UnusedMember.Global
		internal static unsafe bool CompareBitmapFiles(string file1, string file2)
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
				var oBmi = ByteArrayToStructure<BITMAPINFOHEADER>(oBytes, sizeof(BITMAPFILEHEADER));
				var nBmi = ByteArrayToStructure<BITMAPINFOHEADER>(nBytes, sizeof(BITMAPFILEHEADER));
				var equalBmi = oBmi.Equals(nBmi);
				// ReSharper restore UnusedVariable
				var bmp = System.Drawing.Image.FromFile(file1);
				var bmp2 = System.Drawing.Image.FromFile(file2);
			}
			return equal;
		}
	}
}
