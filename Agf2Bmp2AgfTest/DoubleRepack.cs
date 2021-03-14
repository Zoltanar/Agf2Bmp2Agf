using System.IO;
using AGF2BMP2AGF;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Agf2Bmp2AgfTest.Common;

namespace Agf2Bmp2AgfTest
{
	[TestClass]
	public class DoubleRepack
	{
		[TestMethod]
		public void Agf32BppToBmp32Bpp()
		{
			Assert.Inconclusive($"No file for this format found for test: {nameof(Agf32BppToBmp32Bpp)}");
		}

		[TestMethod]
		public void Agf32BppToBmp8Bpp()
		{
			const string fileName = @"SO001";
			RunDoubleRepackTest(fileName);
		}

		[TestMethod]
		public void Agf24BppToBmp24Bpp()
		{
			const string fileName = @"SO074";
			RunDoubleRepackTest(fileName);
		}

		[TestMethod]
		public void Agf24BppToBmp8Bpp()
		{
			const string fileName = @"SO016";
			RunDoubleRepackTest(fileName);
		}

		private static void RunDoubleRepackTest(string fileName)
		{
			Algorithm.CurrentProcessData = new ProcessData();
			//Pack original BMP into AGF
			var originalAgf = GetOriginalAgf(fileName);
			var originalBmpFileName = GetOriginalBmp(fileName);
			var outputAgfFileName = GetOutputAgf(fileName + "DR");
			//packing require first unpacking of the original AGF (data needed is saved to memory rather than file.
			if (!Algorithm.Unpack(originalAgf, null)) Assert.Fail("Failed to unpack file.");
			if (!Algorithm.Pack(originalBmpFileName, outputAgfFileName)) Assert.Fail("Failed to pack file.");

			Algorithm.CurrentProcessData = new ProcessData();
			//Unpack output AGF into new BMP
			var outputBmpFileName = GetOutputBmp(fileName + "DR");
			if (!Algorithm.Unpack(outputAgfFileName, outputBmpFileName)) Assert.Fail("Failed to unpack file.");

			//Compare output BMP against original BMP
			CompareBitmapFiles(originalBmpFileName, outputBmpFileName);

			//Pack output BMP into new AGF
			var doublePackedAgfFileName = GetOutputAgf(fileName + "DR2");
			if (!Algorithm.Pack(outputBmpFileName, doublePackedAgfFileName)) Assert.Fail("Failed to pack file.");

			//Compare first output AGF with new output AGF
			var oBytes = File.ReadAllBytes(outputAgfFileName);
			var nBytes = File.ReadAllBytes(doublePackedAgfFileName);
			var equal = CompareCollection(oBytes, nBytes);
			Assert.IsTrue(equal, "Output AGF files did not match.");
		}
	}
}
