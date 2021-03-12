#include <stdio.h>
#include <stdlib.h>
#ifndef EXAPI
#define EXAPI  extern "C" __declspec(dllexport) 
#endif
EXAPI int __stdcall unlzss(unsigned char* inputbuf, int inputlen, unsigned char* outputbuf, int outputlen);
EXAPI int __stdcall lzss(unsigned char* inputbuf, int inputlen, unsigned char* outputbuf, int outputlen);
EXAPI int __stdcall unlzssTest();
