using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace AGF2BMP2AGF
{
	internal class RunParameters
	{
		private string _errorMessage;
		private string _outputPath;
		private string _inputPath;
		private string _originalAgfPath;
		private string _intermediateBmpPath;

		public ProcessMode Mode { get; set; }
		public bool IsFileMode { get; set; }
		public string InputPath
		{
			get => _inputPath;
			set => _inputPath = Path.GetFullPath(value);
		}
		public string OutputPath
		{
			get => _outputPath;
			set => _outputPath = Path.GetFullPath(value);
		}
		public string OriginalAgfPath
		{
			get => _originalAgfPath;
			set => _originalAgfPath = Path.GetFullPath(value);
		}
		public string IntermediateBmpPath
		{
			get => _intermediateBmpPath;
			set => _intermediateBmpPath = Path.GetFullPath(value);
		}
		public bool Valid { get; set; }
		public string ErrorMessage
		{
			get => _errorMessage;
			set
			{
				Valid = string.IsNullOrWhiteSpace(value);
				_errorMessage = value;
			}
		}
		public string Description =>
			$"{Mode}: [{(IsFileMode ? "F" : "D")}] " +
			$"{Path.GetFileName(InputPath)}->{Path.GetFileName(OutputPath)}" +
			$"{(Mode == ProcessMode.Pack ? $" Original AGF: {Path.GetFileName(OriginalAgfPath)}" : string.Empty)}";

		public RunParameters(string[] argv)
		{
			var @switch = argv[1];
			Mode = argv[1].ToLowerInvariant() switch
			{
				"-u" => ProcessMode.Unpack,
				"-p" => ProcessMode.Pack,
				"-x" => ProcessMode.UnpackAndPack,
				_ => ProcessMode.Invalid
			};
			if (Mode == ProcessMode.Invalid)
			{
				ErrorMessage = $"Invalid switch: {@switch}";
				return;
			}
			InputPath = argv[2];
			bool? isFileMode = File.Exists(InputPath) ? true : Directory.Exists(InputPath) ? false : null;
			if (isFileMode == null)
			{
				ErrorMessage = $"Input File/Directory did not exist: {InputPath}";
				return;
			}
			IsFileMode = isFileMode.Value;
			if (argv.Length > 3 && !string.IsNullOrWhiteSpace(argv[3])) OutputPath = argv[3];
			else
			{
				OutputPath = Mode switch
				{
					ProcessMode.Unpack => IsFileMode ? ReplaceExtension(InputPath, ".BMP") : $"{InputPath}_BMP",
					ProcessMode.Pack => IsFileMode ? ReplaceExtension(InputPath, ".AGF") : $"{InputPath}_X_AGF",
					ProcessMode.UnpackAndPack => IsFileMode ? ReplaceExtension(InputPath, "_X.AGF") : $"{InputPath}_X_AGF",
					_ => throw new ArgumentOutOfRangeException()
				};
			}
			if (Mode == ProcessMode.Pack)
			{
				if (argv.Length > 4 && !string.IsNullOrWhiteSpace(argv[4])) OriginalAgfPath = argv[4];
				else
				{
					Debug.Assert(InputPath != null, nameof(InputPath) + " != null");
					OriginalAgfPath = IsFileMode ? ReplaceExtension(InputPath, ".AGF") :
						Path.Combine(Path.GetDirectoryName(InputPath) ?? throw new InvalidOperationException($"Could not get directory name for {InputPath}"), "AGF");
				}

				if (OriginalAgfPath == OutputPath)
				{
					ErrorMessage = "Original AGF Path and Output Path cannot be the same.";
					return;
				}
			}
			if (Mode == ProcessMode.UnpackAndPack)
			{
				IntermediateBmpPath = IsFileMode ? ReplaceExtension(InputPath,"_X.BMP") : $"{InputPath}_X_BMP";
			}
			Valid = true;
		}

		private string ReplaceExtension(string path, string newExtension)
		{
			var ext = Path.GetExtension(path);
			return path.Substring(0, path.Length - ext.Length) + newExtension;
		}

		public ConvertFileData[] GetFiles()
		{
			if (IsFileMode) return new[] { new ConvertFileData(InputPath, OutputPath, OriginalAgfPath, IntermediateBmpPath) };
			var inputDirectory = new DirectoryInfo(InputPath);
			var outputDirectory = new DirectoryInfo(OutputPath);
			outputDirectory.Create();
			if (Mode == ProcessMode.UnpackAndPack) Directory.CreateDirectory(IntermediateBmpPath);
			var agfDirectory = !string.IsNullOrWhiteSpace(OriginalAgfPath) ? new DirectoryInfo(OriginalAgfPath) : null;
			if (!inputDirectory.Exists) throw new DirectoryNotFoundException("Input directory does not exist.");
			if (Mode == ProcessMode.Pack && (agfDirectory == null || !agfDirectory.Exists)) throw new DirectoryNotFoundException("AGF directory does not exist.");
			var inputExt = Mode == ProcessMode.Pack ? ".BMP" : ".AGF";
			var outputExt = Mode == ProcessMode.Unpack ? ".BMP" : ".AGF";
			var agfExt = ".AGF";
			var inputFiles = inputDirectory.GetFiles($"*{inputExt}", SearchOption.TopDirectoryOnly);
			var list = new List<ConvertFileData>();
			foreach (var file in inputFiles)
			{
				var outputFilePath = Path.Combine(outputDirectory.FullName, Path.GetFileNameWithoutExtension(file.Name) + outputExt);
				switch (Mode)
				{
					case ProcessMode.Pack:
						{
							Debug.Assert(agfDirectory != null, nameof(agfDirectory) + " != null");
							var agfFilePath = Path.Combine(agfDirectory.FullName, Path.GetFileNameWithoutExtension(file.Name) + agfExt);
							if (File.Exists(agfFilePath)) list.Add(new ConvertFileData(file.FullName, outputFilePath, agfFilePath, null));
							else Program.Print(Program.ErrorColor, $"Did not find AGF file for {file.FullName}");
							break;
						}
					case ProcessMode.UnpackAndPack:
						{
							var intermediateBmp = Path.Combine(IntermediateBmpPath, Path.GetFileNameWithoutExtension(file.Name) + ".BMP");
							list.Add(new ConvertFileData(file.FullName, outputFilePath, null, intermediateBmp));
							break;
						}
					default:
						list.Add(new ConvertFileData(file.FullName, outputFilePath, null, null));
						break;
				}
			}
			return list.ToArray();
		}
	}

	internal struct ConvertFileData
	{
		public ConvertFileData(string inputFile, string output, string originalAgf, string intermediateBmp)
		{
			InputFile = inputFile;
			Output = output;
			OriginalAgf = originalAgf;
			IntermediateBmp = intermediateBmp;
		}

		public string InputFile { get; set; }
		public string Output { get; set; }
		public string OriginalAgf { get; set; }
		public string IntermediateBmp { get; set; }

		public void Deconstruct(out string input, out string output, out string originalAgf, out string intermediateBmp)
		{
			input = InputFile;
			output = Output;
			originalAgf = OriginalAgf;
			intermediateBmp = IntermediateBmp;
		}
	}
}