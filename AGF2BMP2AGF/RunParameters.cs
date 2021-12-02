using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AGF2BMP2AGF
{
	internal class RunParameters
	{
		private string _errorMessage;
		private string _outputPath;
		private string _inputPath;
		private string _originalAgfPath;
		private string _intermediateBmpPath;

		private ProcessMode _mode;
		public bool IsFileMode { get; }
		public bool LogErrorsOnly { get; private set; }
		private bool _recursive;
		public bool Parallel { get; private set; } = true;
		private string InputPath
		{
			get => _inputPath;
			set => _inputPath = Path.GetFullPath(value);
		}
		private string OutputPath
		{
			get => _outputPath;
			set => _outputPath = Path.GetFullPath(value);
		}
		private string OriginalAgfPath
		{
			get => _originalAgfPath;
			set => _originalAgfPath = Path.GetFullPath(value);
		}
		private string IntermediateBmpPath
		{
			get => _intermediateBmpPath;
			set => _intermediateBmpPath = Path.GetFullPath(value);
		}
		public bool Valid { get; private set; }
		public string ErrorMessage
		{
			get => _errorMessage;
			private set
			{
				Valid = string.IsNullOrWhiteSpace(value);
				_errorMessage = value;
			}
		}
		public string Description =>
			$"{_mode}: [{(IsFileMode ? "F" : "D")}] " +
			$"{Path.GetFileName(InputPath)}->{Path.GetFileName(OutputPath)}" +
			$"{(_mode == ProcessMode.Pack ? $" (AGF: {Path.GetFileName(OriginalAgfPath)})" : string.Empty)}";
		
		public RunParameters(string[] argv)
		{
			Valid = GetSwitches(argv, out var pathArgs);
			if (!Valid) return;
			InputPath = pathArgs[0];
			bool? isFileMode = File.Exists(InputPath) ? true : Directory.Exists(InputPath) ? false : null;
			if (isFileMode == null)
			{
				ErrorMessage = $"Input File/Directory did not exist: {InputPath}";
				return;
			}
			IsFileMode = isFileMode.Value;
			if (pathArgs.Length > 1 && !string.IsNullOrWhiteSpace(pathArgs[1])) OutputPath = pathArgs[1];
			else
			{
				OutputPath = _mode switch
				{
					ProcessMode.Unpack => IsFileMode ? ReplaceExtension(InputPath, ".BMP") : $"{InputPath}_BMP",
					ProcessMode.Pack => IsFileMode ? ReplaceExtension(InputPath, ".AGF") : $"{InputPath}_X_AGF",
					ProcessMode.UnpackAndPack => IsFileMode ? ReplaceExtension(InputPath, "_X.AGF") : $"{InputPath}_X_AGF",
					_ => throw new ArgumentOutOfRangeException()
				};
			}
			if (_mode == ProcessMode.Pack)
			{
				if (pathArgs.Length > 2 && !string.IsNullOrWhiteSpace(pathArgs[2])) OriginalAgfPath = pathArgs[2];
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
			if (_mode == ProcessMode.UnpackAndPack)
			{
				IntermediateBmpPath = IsFileMode ? ReplaceExtension(InputPath, "_X.BMP") : $"{InputPath}_X_BMP";
			}
			Valid = true;
		}

		private bool GetSwitches(string[] argv, out string[] filePathArgs)
		{
			var filePathList = new List<string>();
			filePathArgs = null;
			//if no switch present, mode is Unpack.
			_mode = ProcessMode.Unpack;
			//flag used to ensure no more than one mode switch is present
			bool isModeSet = false;
			if (argv.Length < 2)
			{
				ErrorMessage = "At least one parameter is required.";
				return false;
			}
			foreach (var argument in argv.Skip(1))
			{
				switch (argument.ToLowerInvariant())
				{
					case "-u":
						if (isModeSet)
						{
							ErrorMessage = "More than one mode switch found, only one argument should indicate process mode.";
							return false;
						}
						_mode = ProcessMode.Unpack;
						isModeSet = true;
						continue;
					case "-p":
						if (isModeSet)
						{
							ErrorMessage = "More than one mode switch found, only one argument should indicate process mode.";
							return false;
						}
						_mode = ProcessMode.Pack;
						isModeSet = true;
						continue;
					case "-x":
						if (isModeSet)
						{
							ErrorMessage = "More than one mode switch found, only one argument should indicate process mode.";
							return false;
						}
						_mode = ProcessMode.UnpackAndPack;
						isModeSet = true;
						continue;
					case "-e":
						LogErrorsOnly = true;
						continue;
					case "-r":
						_recursive = true;
						continue;
					case "-sc":
						Parallel = false;
						continue;
					default:
						if (argument.StartsWith("-"))
						{
							ErrorMessage = $"Unrecognized switch : {argument}";
							return false;
						}
						filePathList.Add(argument);
						continue;
				}
			}
			filePathArgs = filePathList.ToArray();
			return true;
		}

		private static string ReplaceExtension(string path, string newExtension)
		{
			var ext = Path.GetExtension(path);
			return path.Substring(0, path.Length - ext.Length) + newExtension;
		}

		public ConvertFileData[] GetFiles()
		{
			if (IsFileMode) return new[] { new ConvertFileData(InputPath, OutputPath, OriginalAgfPath, IntermediateBmpPath, _mode, null, null, null, 1) };
			var inputDirectory = new DirectoryInfo(InputPath);
			var outputDirectory = new DirectoryInfo(OutputPath);
			outputDirectory.Create();
			if (_mode == ProcessMode.UnpackAndPack) Directory.CreateDirectory(IntermediateBmpPath);
			var agfDirectory = !string.IsNullOrWhiteSpace(OriginalAgfPath) ? new DirectoryInfo(OriginalAgfPath) : null;
			if (!inputDirectory.Exists) throw new DirectoryNotFoundException("Input directory does not exist.");
			if (_mode == ProcessMode.Pack && (agfDirectory == null || !agfDirectory.Exists)) throw new DirectoryNotFoundException("AGF directory does not exist.");
			var inputExt = _mode == ProcessMode.Pack ? ".BMP" : ".AGF";
			var outputExt = _mode == ProcessMode.Unpack ? ".BMP" : ".AGF";
			const string agfExt = ".AGF";
			var inputFiles = inputDirectory.GetFiles($"*{inputExt}", _recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
			var list = new List<ConvertFileData>();
			int index = 0;
			foreach (var file in inputFiles)
			{
				index++;
				var outputFilePath = ReplacePath(file.FullName, inputDirectory.FullName, outputDirectory.FullName, outputExt, out var partialInputPath, out var partialOutputPath);
				switch (_mode)
				{
					case ProcessMode.Pack:
						{
							Debug.Assert(agfDirectory != null, nameof(agfDirectory) + " != null");
							var agfFilePath = ReplacePath(file.FullName, inputDirectory.FullName, agfDirectory.FullName, agfExt, out _, out var partialAgfPath);
							if (File.Exists(agfFilePath)) list.Add(new ConvertFileData(file.FullName, outputFilePath, agfFilePath, null, _mode, partialInputPath, partialOutputPath, partialAgfPath, index));
							else Program.Print(Program.ErrorColor, $"Did not find AGF file for {file.FullName}");
							break;
						}
					case ProcessMode.UnpackAndPack:
						{
							var intermediateBmp = Path.Combine(IntermediateBmpPath, Path.GetFileNameWithoutExtension(file.Name) + ".BMP");
							list.Add(new ConvertFileData(file.FullName, outputFilePath, null, intermediateBmp, _mode, partialInputPath, partialOutputPath, null, index));
							break;
						}
					default:
						list.Add(new ConvertFileData(file.FullName, outputFilePath, null, null, _mode, partialInputPath, partialOutputPath, null, index));
						break;
				}
			}
			return list.ToArray();
		}

		private static string ReplacePath(string inputFile, string inputDirectory, string outputDirectory,
			string outputExt, out string partialInputPath, out string partialOutputPath)
		{
			partialInputPath = Path.GetFileName(inputFile);
			partialOutputPath = ReplaceExtension(partialInputPath, outputExt);
			var outputFilePath = Path.Combine(outputDirectory ?? inputDirectory, partialOutputPath);
			return outputFilePath;
		}
	}

	internal class ConvertFileData
	{
		public ConvertFileData(string input, string output, string originalAgf, string intermediateBmp,
			ProcessMode processMode, 
			string partialInput, string partialOutput, string partialAgf, 
			int index)
		{
			Input = input;
			Output = output;
			OriginalAgf = originalAgf;
			IntermediateBmp = intermediateBmp;
			Mode = processMode;
			PartialInput = partialInput ?? Path.GetFileName(input);
			PartialOutput = partialOutput ?? Path.GetFileName(output);
			PartialAgf = partialAgf ?? (!string.IsNullOrWhiteSpace(originalAgf) ? Path.GetFileName(originalAgf) : string.Empty);
			Index = index;
		}

		public string Input { get; }
		public string Output { get; }
		public string OriginalAgf { get; }
		public string IntermediateBmp { get; }
		private string PartialInput { get; }
		private string PartialOutput { get; }
		private string PartialAgf { get; }
		public ProcessMode Mode { get; }
		public int Index { get; }
		public ProcessData ProcessData { get; } = new ();

		public void Deconstruct(out string input, out string output, out string originalAgf, out string intermediateBmp)
		{
			input = Input;
			output = Output;
			originalAgf = OriginalAgf;
			intermediateBmp = IntermediateBmp;
		}

		public string GetDescription(int? filesLength, string formatString)
		{
			var agfString = Mode == ProcessMode.Pack ? $" (AGF: {PartialAgf})" : string.Empty;
			return filesLength.HasValue 
				? $"\t{Index.ToString(formatString)}/{filesLength} {PartialInput}->{PartialOutput}{agfString}" 
				: $"\t{Index} {PartialInput}->{PartialOutput}{agfString}";
		}
	}
}