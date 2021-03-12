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

		public ProcessMode Mode { get; set; }
		public bool IsFileMode { get; }
		public bool LogErrorsOnly { get; set; }
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
			if (Mode == ProcessMode.UnpackAndPack)
			{
				IntermediateBmpPath = IsFileMode ? ReplaceExtension(InputPath,"_X.BMP") : $"{InputPath}_X_BMP";
			}
			Valid = true;
		}

		private bool GetSwitches(IEnumerable<string> argv, out string[] filePathArgs)
		{
			var filePathList = new List<string>();
			filePathArgs = null;
			//if no switch present, mode is Unpack.
			Mode = ProcessMode.Unpack;
			//flag used to ensure no more than one mode switch is present
			bool isModeSet = false;
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
						Mode = ProcessMode.Unpack;
						isModeSet = true;
						continue;
					case "-p":
						if (isModeSet)
						{
							ErrorMessage = "More than one mode switch found, only one argument should indicate process mode.";
							return false;
						}
						Mode = ProcessMode.Pack;
						isModeSet = true;
						continue;
					case "-x":
						if (isModeSet)
						{
							ErrorMessage = "More than one mode switch found, only one argument should indicate process mode.";
							return false;
						}
						Mode = ProcessMode.UnpackAndPack;
						isModeSet = true;
						continue;
					case "-e":
						LogErrorsOnly = true;
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
			if (IsFileMode) return new[] { new ConvertFileData(InputPath, OutputPath, OriginalAgfPath, IntermediateBmpPath, Mode) };
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
							if (File.Exists(agfFilePath)) list.Add(new ConvertFileData(file.FullName, outputFilePath, agfFilePath, null, Mode));
							else Program.Print(Program.ErrorColor, $"Did not find AGF file for {file.FullName}");
							break;
						}
					case ProcessMode.UnpackAndPack:
						{
							var intermediateBmp = Path.Combine(IntermediateBmpPath, Path.GetFileNameWithoutExtension(file.Name) + ".BMP");
							list.Add(new ConvertFileData(file.FullName, outputFilePath, null, intermediateBmp, Mode));
							break;
						}
					default:
						list.Add(new ConvertFileData(file.FullName, outputFilePath, null, null, Mode));
						break;
				}
			}
			return list.ToArray();
		}
	}

	internal struct ConvertFileData
	{
		public ConvertFileData(string input, string output, string originalAgf, string intermediateBmp,
			ProcessMode processMode)
		{
			Input = input;
			Output = output;
			OriginalAgf = originalAgf;
			IntermediateBmp = intermediateBmp;
			Mode = processMode;
		}

		public ProcessMode Mode { get; set; }

		public string Input { get; set; }
		public string Output { get; set; }
		public string OriginalAgf { get; set; }
		public string IntermediateBmp { get; set; }
		
		public void Deconstruct(out string input, out string output, out string originalAgf, out string intermediateBmp)
		{
			input = Input;
			output = Output;
			originalAgf = OriginalAgf;
			intermediateBmp = IntermediateBmp;
		}

		public string GetDescription(int index, int filesLength,string formatString)
		{
			return $"\t{index.ToString(formatString)}/{filesLength} {Path.GetFileName(Input)}->{Path.GetFileName(Output)}" +
			       $"{(Mode == ProcessMode.Pack ? $" Original AGF: {Path.GetFileName(OriginalAgf)}" : string.Empty)}";
		}
	}
}