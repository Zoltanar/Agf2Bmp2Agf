using System;
using System.Diagnostics;
using System.Linq;
// ReSharper disable InconsistentNaming

namespace AGF2BMP2AGF
{
	public static class Program
	{
		public const ConsoleColor ErrorColor = ConsoleColor.Red;
        public const ConsoleColor WarningColor = ConsoleColor.Yellow;
        public const ConsoleColor SuccessColor = ConsoleColor.Green;
        public static volatile int ParallelErrors;

		private static string Version
		{
			get
			{
				var ver = typeof(Program).Assembly.GetName().Version;
				return $"v{ver.Major}.{ver.Minor}";
			}
		}

		private static bool RewriteWithNextLine { get; set; }

		public static void Print(ConsoleColor color, string message, bool rewriteWithNextLine = false)
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
			var runParameters = new RunParameters(argv);
			if (!runParameters.Valid)
			{
				Print(ErrorColor, runParameters.ErrorMessage);
				PrintHelp(argv[0]);
				return -1;
			}
			Print(WarningColor, runParameters.Description);
			var errors = Run(runParameters);
			watch.Stop();
			if (runParameters.IsFileMode) return errors;
			var completedString = $"Completed in {watch.Elapsed:hh\\:mm\\:ss\\.ffff}";
			if (errors > 0) Print(ErrorColor, $"{completedString} ({errors} errors)");
			else Print(SuccessColor, completedString);
			return errors;
		}

		private static int Run(RunParameters runParameters)
		{
			var files = runParameters.GetFiles();
			int errors = 0;
			string formatString = new string(Enumerable.Repeat('0', files.Length.ToString().Length).ToArray());
			if (runParameters.Parallel)
			{
				ParallelErrors = 0;
				files.AsParallel().ForAll(data => ProcessFileParallel(data, runParameters.LogErrorsOnly));
				errors = ParallelErrors;
			}
			else
			{
				foreach (var file in files)
				{
					//if it returns false, that means user stopped it.
					if (!ProcessFile(runParameters, files.Length, formatString, file, ref errors)) return errors;
				}
			}
			return errors;
		}

		private static bool ProcessFile(RunParameters runParameters, int fileCount, string formatString,
			ConvertFileData file, ref int errors)
		{
			if (fileCount > 1)
			{
				if (Console.KeyAvailable)
				{
					//read key available to skip
					Console.ReadKey(true);
					Print(WarningColor, "Key pressed, press Escape to stop or any other key to continue...");
					var key = Console.ReadKey(true);
					if (key.Key == ConsoleKey.Escape) return false;
				}
				if (runParameters.LogErrorsOnly) Print(WarningColor, $"Processing file {file.Index.ToString(formatString)}/{fileCount}", true);
				else Print(WarningColor, file.GetDescription(fileCount, formatString));
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
				if (!result) errors++;
			}
			catch (Exception ex)
			{
				errors++;
				Print(ErrorColor, $"\t{file.GetDescription(fileCount, formatString)} - Failed: {ex}");
			}

			return true;
		}

		private static void ProcessFileParallel(ConvertFileData file, bool logErrorsOnly)
		{
			try
			{
				var success = file.Mode switch
				{
					ProcessMode.Unpack => Unpack(file),
					ProcessMode.Pack => Pack(file),
					ProcessMode.UnpackAndPack => UnpackAndPack(file),
					_ => throw new ArgumentOutOfRangeException()
				};
				if(!success || !logErrorsOnly) Print(success ? SuccessColor : ErrorColor, $"\t{file.GetDescription(null, null)}");
			}
			catch (Exception ex)
			{
				Print(ErrorColor, $"\t{file.GetDescription(null,null)} - Failed: {ex}");
			}
		}

		private static bool UnpackAndPack(ConvertFileData file)
		{
			var (inputFile, outputFile, _, intermediateBmp) = file;
			return Algorithm.Unpack(inputFile, intermediateBmp, file.ProcessData) && Algorithm.Pack(intermediateBmp, outputFile, file.ProcessData);
		}

		private static bool Pack(ConvertFileData file)
		{
			var (inputFile, outputFile, agfFile, _) = file;
			return Algorithm.Unpack(agfFile, null, file.ProcessData) && Algorithm.Pack(inputFile, outputFile, file.ProcessData);
		}

		private static bool Unpack(ConvertFileData file)
		{
			var (inputFile, outputFile, _, _) = file;
			return Algorithm.Unpack( inputFile, outputFile, file.ProcessData);
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
         -r  Recursive mode  (affects directory mode only) process files in subdirectories and output with same directory structure
         -sc force processing to use a single thread only, rather than in parallel
  
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
