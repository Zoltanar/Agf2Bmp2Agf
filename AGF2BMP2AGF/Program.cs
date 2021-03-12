using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static AGF2BMP2AGF.Algorithm;

// ReSharper disable InconsistentNaming


namespace AGF2BMP2AGF
{
	public static class Program
	{
		internal const ConsoleColor ErrorColor = ConsoleColor.Red;
		private const ConsoleColor WarningColor = ConsoleColor.Yellow;
		internal const ConsoleColor SuccessColor = ConsoleColor.Green;

		public static string Version
		{
			get
			{
				var ver = typeof(Program).Assembly.GetName().Version;
				return $"v{ver.Major}.{ver.Minor}";
			}
		}

		public static bool RewriteWithNextLine { get; set; }

		internal static void Print(ConsoleColor color, string message, bool rewriteWithNextLine = false)
		{
			if (RewriteWithNextLine)
			{
				//go back to left and write blank line
				Console.CursorLeft = 0;
				Console.Write(new string(Enumerable.Repeat(' ', Console.WindowWidth - 1).ToArray()));
				//go back to left to overwrite blank line with new line
				Console.CursorLeft = 0;
			}
			RewriteWithNextLine = rewriteWithNextLine;
			Console.ForegroundColor = color;
			Console.Write(message);
			if (!rewriteWithNextLine) Console.WriteLine();
			Console.ResetColor();
		}

		private static int Main()
		{
			var argv = Environment.GetCommandLineArgs();
			int res = -1;
			try
			{
				res = Main2(argv);
			}
			catch (Exception ex)
			{
				Print(ErrorColor, ex.ToString());
			}
#if DEBUG
			Console.WriteLine("Press any key to exit...");
			Console.ReadKey(true);
#endif
			return res;
		}

		private static int Main2(string[] argv)
		{
			var watch = Stopwatch.StartNew();
			if (argv.Length < 2)
			{
				PrintHelp(argv[0]);
				return -1;
			}
			argv = argv.Length == 2 ? new[] { argv[0], "-u", argv[1] } : argv;
			var runParameters = new RunParameters(argv);
			if (!runParameters.Valid)
			{
				Print(ErrorColor, runParameters.ErrorMessage);
				PrintHelp(argv[0]);
				return -1;
			}
			Print(WarningColor, runParameters.Description);
			var result = Run(runParameters);
			watch.Stop();
			if (!runParameters.IsFileMode) Print(result == 0 ? SuccessColor : ErrorColor, $"Completed in {watch.Elapsed:hh\\:mm\\:ss\\.ffff}");
			return result;
		}

		private static int Run(RunParameters runParameters)
		{
			var files = runParameters.GetFiles();
			bool anyFailure = false;
			int index = 0;
			string formatString = new string(Enumerable.Repeat('0', files.Length.ToString().Length).ToArray());
			foreach (var file in files)
			{
				index++;
				//reset current data.
				CurrentProcessData = new ProcessData();
				if (files.Length > 1)
				{
					if (runParameters.LogErrorsOnly && runParameters.IsFileMode) Print(WarningColor, $"Processing file {index.ToString(formatString)}/{files.Length}", true);
					else Print(WarningColor, file.GetDescription(index, files.Length, formatString));
				}
				try
				{
					var result = file.Mode switch
					{
						ProcessMode.Unpack => Unpack(file),
						ProcessMode.Pack => Pack(file),
						ProcessMode.UnpackAndPack => UnpackAndPack(file),
						_ => throw new ArgumentOutOfRangeException()
					};
					if (result != 0) anyFailure = true;
				}
				catch (Exception ex)
				{
					anyFailure = true;
					Print(ErrorColor, $"{file.GetDescription(index, files.Length, formatString)} - Failed: {ex}");
				}
			}
			return anyFailure ? -1 : 0;
		}

		private static int UnpackAndPack(ConvertFileData file)
		{
			var (inputFile, outputFile, _, intermediateBmp) = file;
			int inputFileHandle = OpenFileOrDie(inputFile, FileMode.Open);
			Algorithm.Unpack(inputFileHandle, inputFile, intermediateBmp);
			int unpackedFileHandle = OpenFileOrDie(intermediateBmp, FileMode.Open);
			int xAgfF = OpenFileOrDie(outputFile, FileMode.Create);
			return Algorithm.Pack(unpackedFileHandle, xAgfF);
		}

		private static int Pack(ConvertFileData file)
		{
			var (inputFile, outputFile, agfFile, _) = file;
			int agfFileHandle = OpenFileOrDie(agfFile, FileMode.Open);
			Algorithm.Unpack(agfFileHandle, agfFile, null);
			int inputFileHandle = OpenFileOrDie(inputFile, FileMode.Open);
			int outputFileHandle = OpenFileOrDie(outputFile, FileMode.Create);
			return Algorithm.Pack(inputFileHandle, outputFileHandle);
		}

		private static int Unpack(ConvertFileData file)
		{
			var (inputFile, outputFile, _, _) = file;
			int fd = OpenFileOrDie(inputFile, FileMode.Open);
			return Algorithm.Unpack(fd, inputFile, outputFile);
		}

		private static void PrintHelp(string thisFile)
		{
			string help =
				// ReSharper disable StringLiteralTypo
				$@"agf2bmp2agf ({Version}) by Zoltanar, modified from asmodean's agf2bmp
Using LZSS compression by Haruhiko Okumura modified by Shawn Hargreaves and Xuan (LzssCpp.dll)

Usage: {thisFile} [switches] <input> [output] [original_agf]
       or {thisFile} <input> (for unpacking)
  mode switches, only one possible, defaults to Pack if omitted:
         -p  Pack input file(s) of BMP type into AGF format
         -u  Unpack input file(s) of AGF type into BMP format
         -x  Unpacks input file(s) of AGF type into BMP format, then repacks them back

  other switches, not mandatory:
         -e  Log errors only (affects directory mode only)
  
  input: path to input file or directory (in which case all BMP files in directory will be the inputs).

  output:  path to output file or directory, if input is a directory, this must also be a directory, if omitted:
           in file mode it will use the same file name but change extension (e.g: MyFile.AGF),
           in directory mode it will create a directory with same name but with the output type suffixed,
           _X_AGF for packing (to prevent overwriting existing files) and _BMP for unpacking

  original_agf:  not required if unpacking.
                 this argument specifies location of original AGF files, if omitted:
                 in file mode it will use the same file name but with AGF extension,
                 in directory mode it will use the input directory
	
	Search for files in directory is recursive so files in subfolders are included and structure is maintained in output directory.
	When packing, output and original_agf must not be the same in order to prevent overwriting files.
	Original AGF files are required to pack BMP files back into same format.";
			// ReSharper restore StringLiteralTypo
			Print(WarningColor, help);
		}
	}

	internal enum ProcessMode
	{
		Unpack = 1,
		Pack = 2,
		UnpackAndPack = 3
	}
}
