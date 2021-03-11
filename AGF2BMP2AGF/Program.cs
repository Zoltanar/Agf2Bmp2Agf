using System;
using System.IO;
using static AGF2BMP2AGF.Algorithm;
#if DEBUG
using System.Linq;
using System.Text.RegularExpressions;
#endif

// ReSharper disable InconsistentNaming

namespace AGF2BMP2AGF
{
	internal static class Program
	{
		internal const ConsoleColor ErrorColor = ConsoleColor.Red;
		private const ConsoleColor WarningColor = ConsoleColor.Yellow;

#if DEBUG
		private static readonly Regex ArgsParser = new(" *[^\";]* *| *\"[^\";]*\" *", RegexOptions.Compiled);
#endif

		internal static void Print(ConsoleColor color, string message, params object[] formatted)
		{
			Console.ForegroundColor = color;
			Console.WriteLine(message, formatted);
			Console.ResetColor();
		}

		private static int Main()
		{
			var argv = Environment.GetCommandLineArgs();
			int res = -1;
#if DEBUG
			while (true)
			{
#endif
				try
				{
					res = Main2(argv);
				}
				catch (Exception ex)
				{
					Print(ErrorColor, ex.ToString());
				}
#if DEBUG
				argv = new[] { argv[0], null, null, null, null, };
				Console.WriteLine("DEBUG: Enter new argument string...");
				var newArgsIn = Console.ReadLine();
				if (string.IsNullOrWhiteSpace(newArgsIn)) return res;
				var matches = ArgsParser.Matches(newArgsIn);
				var newArgs = matches.Cast<Match>().Where(m => !string.IsNullOrWhiteSpace(m.Value)).Take(4).Select(v => v.Value).ToArray();
				for (int i = 0; i < newArgs.Length; i++)
				{
					argv[i + 1] = newArgs[i].Trim();
				}
			}
#endif
			return res;
		}

		private static int Main2(string[] argv)
		{
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
			return result;
		}

		private static int Run(RunParameters runParameters)
		{
			var files = runParameters.GetFiles();
			return runParameters.Mode switch
			{
				ProcessMode.Unpack => Unpack(files),
				ProcessMode.Pack => Pack(files),
				ProcessMode.UnpackAndPack => UnpackAndPack(files),
				_ => throw new ArgumentOutOfRangeException()
			};
		}

		private static int UnpackAndPack(ConvertFileData[] files)
		{
			bool anyFailure = false;
			foreach (var (inputFile, outputFile, _, intermediateBmp) in files)
			{
				CurrentProcessData = new ProcessData();
				int inputFileHandle = OpenFileOrDie(inputFile, FileMode.Open);
				Algorithm.Unpack(inputFileHandle, inputFile, intermediateBmp);
				int unpackedFileHandle = OpenFileOrDie(intermediateBmp, FileMode.Open);
				int xAgfF = OpenFileOrDie(outputFile, FileMode.Create);
				var result = Algorithm.Pack(unpackedFileHandle, xAgfF);
				if (result != 0) anyFailure = true;
			}
			return anyFailure ? -1 : 0;
		}

		private static int Pack(ConvertFileData[] files)
		{
			bool anyFailure = false;
			foreach (var (inputFile, outputFile, agfFile, _) in files)
			{
				Algorithm.CurrentProcessData = new ProcessData();
				int agfFileHandle = OpenFileOrDie(agfFile, FileMode.Open);
				Algorithm.Unpack(agfFileHandle, agfFile, null);
				int inputFileHandle = OpenFileOrDie(inputFile, FileMode.Open);
				int outputFileHandle = OpenFileOrDie(outputFile, FileMode.Create);
				var result = Algorithm.Pack(inputFileHandle, outputFileHandle);
				if (result != 0) anyFailure = true;
			}
			return anyFailure ? -1 : 0;
		}

		private static int Unpack(ConvertFileData[] files)
		{
			bool anyFailure = false;
			foreach (var (inputFile, outputFile, _, _) in files)
			{
				Algorithm.CurrentProcessData = new ProcessData();
				int fd = OpenFileOrDie(inputFile, FileMode.Open);
				var result = Algorithm.Unpack(fd, inputFile, outputFile);
				if (result != 0) anyFailure = true;
			}
			return anyFailure ? -1 : 0;
		}

		private static string Version
		{
			get
			{
				var ver = typeof(Program).Assembly.GetName().Version;
				return $"v{ver.Major}.{ver.Minor}";
			}
		}

		private static void PrintHelp(string thisFile)
		{
			string help =
				// ReSharper disable StringLiteralTypo
				$@"agf2bmp2agf ({Version}) by Zoltanar, modified from asmodean's agf2bmp
Using LZSS compression by Haruhiko Okumura modified by Shawn Hargreaves and Xuan (LzssCpp.dll)

Usage: {thisFile} <switch> <input> [output] [original_agf]
       or {thisFile} <input> (for unpacking)
switch:
	-p	Pack input file(s) of BMP type into AGF format
	-u	Unpack input file(s) of AGF type into BMP format
	-x	Unpacks input file(s) of AGF type into BMP format, then repacks them back

	input: path to input file or directory (in which case all BMP files in directory will be the inputs).

	output: path to output file or directory, if input is a directory, this must also be a directory, if omitted:
				  in file mode it will use the same file name but change extension (e.g: MyFile.AGF),
				  in directory mode it will create a directory with same name but with the output type suffixed,
				  _X_AGF for packing (to prevent overwriting existing files) and _BMP for unpacking

	original_agf: not required if unpacking.
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
		Invalid = 0,
		Unpack = 1,
		Pack = 2,
		UnpackAndPack = 3
	}
}
