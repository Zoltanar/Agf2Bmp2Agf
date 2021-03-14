using AGF2BMP2AGF;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Agf2Bmp2AgfTest.Common;

namespace Agf2Bmp2AgfTest
{
	[TestClass]
	public class Compatibility
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
			RunUnpackTest(fileName);
		}

		[TestMethod]
		public void Agf24BppToBmp24Bpp()
		{
			const string fileName = @"SO074";
			RunUnpackTest(fileName);
		}

		[TestMethod]
		public void Agf24BppToBmp8Bpp()
		{
			const string fileName = @"SO016";
			RunUnpackTest(fileName);
		}

		private static void RunUnpackTest(string fileName)
		{
			Algorithm.CurrentProcessData = new ProcessData();
			var agfFileName = GetOriginalAgf(fileName);
			var outputBmpFileName = GetOutputBmp(fileName);
			var originalBmpFileName = GetOriginalBmp(fileName);
			Algorithm.Unpack(agfFileName, outputBmpFileName);
			CompareBitmapFiles(originalBmpFileName, outputBmpFileName);
		}

	}
}
