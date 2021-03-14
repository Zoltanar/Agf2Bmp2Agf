using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AGF2BMP2AGF;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static AGF2BMP2AGF.Operation;

namespace Agf2Bmp2AgfTest
{
	public static class Common
	{
		private const string BmpOutputFolder = @"Output BMP";
		private const string AgfOutputFolder = @"Output AGF";

		static Common()
		{
			Directory.CreateDirectory(BmpOutputFolder);
			Directory.CreateDirectory(AgfOutputFolder);
		}
		public static string GetOutputBmp(string fileName)
		{
			return Path.Combine(BmpOutputFolder, $"{fileName}.BMP");
		}

		public static string GetOutputAgf(string fileName)
		{
			return Path.Combine(AgfOutputFolder, $"{fileName}.AGF");
		}

		public static string GetOriginalAgf(string fileName)
		{
			var file = new FileInfo($"Original AGF\\{fileName}.AGF");
			Assert.IsTrue(file.Exists, $"Original AGF file does not exist: {file.FullName}");
			return file.FullName;
		}

		public static string GetOriginalBmp(string fileName)
		{
			var file = new FileInfo($"Original BMP\\{fileName}.BMP");
			Assert.IsTrue(file.Exists, $"Original BMP file does not exist: {file.FullName}");
			return file.FullName;
		}

		public static void CompareBitmapFiles(string file1, string file2)
		{
			var oBytes = File.ReadAllBytes(file1);
			var nBytes = File.ReadAllBytes(file2);
			var equal = CompareCollection(oBytes, nBytes);
			if (!equal)
			{
				var oBmf = ByteArrayToStructure<BITMAPFILEHEADER>(oBytes);
				var nBmf = ByteArrayToStructure<BITMAPFILEHEADER>(nBytes);
				Assert.AreEqual(oBmf, nBmf, "Bitmap File Headers did not match.");
				var oBmi = ByteArrayToStructure<BITMAPINFOHEADER>(oBytes, Marshal.SizeOf<BITMAPFILEHEADER>());
				var nBmi = ByteArrayToStructure<BITMAPINFOHEADER>(nBytes, Marshal.SizeOf<BITMAPFILEHEADER>());
				Assert.AreEqual(oBmi, nBmi, "Bitmap File Headers did not match.");
			}
			Assert.IsTrue(equal, "Unpacked BMP file did not match BMP file unpacked by original tool.");
		}

		public static bool CompareCollection<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
		{
			if (a.Count != b.Count) return false;
			if (a.SequenceEqual(b)) return true;
			int i = 0;
			int indexOfMismatch = -1;
			while (i < a.Count)
			{
				if (!a[i].Equals(b[i]))
				{
					indexOfMismatch = i;
					Trace.WriteLine($"Files did not match at byte {i}.");
					break;
				}
				i++;
			}
			return indexOfMismatch == -1;
		}
	}
}
