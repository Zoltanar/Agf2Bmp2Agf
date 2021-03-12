using System.Collections.Generic;
using System.Diagnostics;
using AGF2BMP2AGF;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static AGF2BMP2AGF.Operation;

namespace Agf2Bmp2AgfTest
{
	[TestClass]
	public class Compatibility
	{
		private const string OutputFolder = @"Output BMP";

		static Compatibility()
		{
			Directory.CreateDirectory(OutputFolder);
		}

		[TestMethod]
		public void Agf32BppToBmp32Bpp()
		{
			Assert.Inconclusive($"No file for this format found for test: {nameof(Agf32BppToBmp32Bpp)}");
		}

		[TestMethod]
		public void Agf32BppToBmp8Bpp()
		{
			const string fileName = @"SO001";
			RunPackTest(fileName);
		}

		[TestMethod]
		public void Agf24BppToBmp24Bpp()
		{
			const string fileName = @"SO074";
			RunPackTest(fileName);
		}

		[TestMethod]
		public void Agf24BppToBmp8Bpp()
		{
			const string fileName = @"SO016";
			RunPackTest(fileName);
		}

		private static void RunPackTest(string fileName)
		{
			var agfFileName = GetOriginalAgf(fileName);
			var outputBmpFileName = GetOutputBmp(fileName);
			var originalBmpFileName = GetOriginalBmp(fileName);
			var fileHandle = Algorithm.OpenFileOrDie(agfFileName, FileMode.Open);
			Algorithm.Unpack(fileHandle, agfFileName, outputBmpFileName);
			CompareBitmapFiles(originalBmpFileName, outputBmpFileName);
		}

		private static string GetOutputBmp(string fileName)
		{
			return Path.Combine(OutputFolder, $"{fileName}.BMP");
		}

		private static string GetOriginalAgf(string fileName)
		{
			var file = new FileInfo($"Original AGF\\{fileName}.AGF");
			Assert.IsTrue(file.Exists, $"Original AGF file does not exist: {file.FullName}");
			return file.FullName;
		}

		private static string GetOriginalBmp(string fileName)
		{
			var file = new FileInfo($"Original BMP\\{fileName}.BMP");
			Assert.IsTrue(file.Exists, $"Original BMP file does not exist: {file.FullName}");
			return file.FullName;
		}
		
		private static void CompareBitmapFiles(string file1, string file2)
		{
			var oBytes = File.ReadAllBytes(file1);
			var nBytes = File.ReadAllBytes(file2);
			var equal = CompareCollection(oBytes, nBytes);
			if (!equal)
			{
				var oBmf = ByteArrayToStructure<BITMAPFILEHEADER>(oBytes);
				var nBmf = ByteArrayToStructure<BITMAPFILEHEADER>(nBytes);
				Assert.AreEqual(oBmf, nBmf, "Bitmap File Headers did not match.");
				var oBmi = ByteArrayToStructure<BITMAPINFOHEADER>(oBytes,Marshal.SizeOf<BITMAPFILEHEADER>());
				var nBmi = ByteArrayToStructure<BITMAPINFOHEADER>(nBytes, Marshal.SizeOf<BITMAPFILEHEADER>());
				Assert.AreEqual(oBmi, nBmi, "Bitmap File Headers did not match.");
			}
			Assert.IsTrue(equal, "Unpacked BMP file did not match BMP file unpacked by original tool.");
		}

		private static bool CompareCollection<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
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
