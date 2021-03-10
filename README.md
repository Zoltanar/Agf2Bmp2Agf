# Agf2Bmp2Agf
agf2bmp2agf by Zoltanar, modified from asmodean's agf2bmp  
Using LZSS compression by Haruhiko Okumura modified by Shawn Hargreaves and Xuan (LzssCpp.dll)  
Convert AGF files (used in Eushully games) to BMP and then back.  
Requires original AGF files to convert from BMP to AGF (for now).  
  
Usage Examples:

Unpack AGF file to BMP:  
`Agf2Bmp2Agf -u SO001.AGF SO001.BMP`

Pack edited BMP to AGF (must give path to original AGF file):  
`Agf2Bmp2Agf -p Edited\SO001.BMP "Edited AGF\SO001.AGF" "SO001.AGF"`

Unpack folder of AGF to BMP:  
`Agf2Bmp2Agf -u "C:\AGF" "C:\BMP";`

Pack folder of edited BMP files to AGF:  
`Agf2Bmp2Agf -p "C:\Edited BMP" "C:\Edited AGF" "C:\AGF";`
  
```
Usage: agf2bmp2agf <switch> <input> [output] [original_agf]

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
```

Search for files in directory is recursive so files in subfolders are included and structure is maintained in output directory.  
When packing, output and original_agf must not be the same in order to prevent overwriting files.  
Original AGF files are required to pack BMP files back into same format.  
  
  
