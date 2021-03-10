#include <stdio.h>
#include <stdlib.h>
#ifndef EXAPI
#define EXAPI  extern "C" __declspec(dllexport) 
#endif
int lzss(unsigned char*, int, unsigned char*, int);
int unlzss(unsigned char*, int, unsigned char*, int);
EXAPI int __stdcall unlzss(unsigned char* inputbuf, unsigned char* outputbuf, int* inputlen, int* outputlen);
EXAPI int __stdcall lzss(unsigned char* inputbuf, int* inputlen, unsigned char* outputbuf, int* outputlen);
EXAPI int __stdcall unlzssTest();
