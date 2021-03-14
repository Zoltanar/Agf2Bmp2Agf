using AGF2BMP2AGF;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Agf2Bmp2AgfTest.Common;

namespace Agf2Bmp2AgfTest
{
	[TestClass]
	public class Compatibility
	{
		[TestMethod]
		public void AgfToMpeg()
		{
			// ReSharper disable once UnusedVariable
			const string fileName = @"CSB035BF";
			Assert.Inconclusive("Not implemented.");
		}
		[TestMethod]
		public void Agf32BppToBmp32Bpp()
		{
			const string fileName = @"CSB035BF";
			RunUnpackTest(fileName);
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
			var processData = new ProcessData();
			var agfFileName = GetOriginalAgf(fileName);
			var outputBmpFileName = GetOutputBmp(fileName);
			var originalBmpFileName = GetOriginalBmp(fileName);
			Algorithm.Unpack(agfFileName, outputBmpFileName, processData);
			CompareBitmapFiles(originalBmpFileName, outputBmpFileName);
		}

	}
}
