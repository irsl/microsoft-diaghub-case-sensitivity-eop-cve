// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"

#include <stdio.h>


void my_debug(const char * m) {
	FILE* fd = NULL;
	if (!fopen_s(&fd, "C:\\dll.log", "a")) {
		fprintf(fd, "%s\n", m);
		fclose(fd);
	}
}


extern "C" __declspec(dllexport) void ApiSetQueryApiSetPresence() 
{
	my_debug("ApiSetQueryApiSetPresence");
}

/*
extern "C" __declspec(dllexport) void DeviceCapabilitiesX()
{
	my_debug("DeviceCapabilitiesW");
}


extern "C" __declspec(dllexport) void GetFormW()
{
	my_debug("GetFormW");
}

extern "C" __declspec(dllexport) void SetPrinterW()
{
	my_debug("SetPrinterW");
}

extern "C" __declspec(dllexport) void OpenPrinterW()
{
	my_debug("OpenPrinterW");
}

extern "C" __declspec(dllexport) void ClosePrinter()
{
	my_debug("ClosePrinter");
}

extern "C" __declspec(dllexport) void GetPrinterDataExW()
{
	my_debug("GetPrinterDataExW");
}

extern "C" __declspec(dllexport) void GetPrinterDriverDirectoryW()
{
	my_debug("GetPrinterDriverDirectoryW");
}

extern "C" __declspec(dllexport) void GetPrinterDataW()
{
	my_debug("GetPrinterDataW");
}

extern "C" __declspec(dllexport) void OpenPrinter2W()
{
	my_debug("OpenPrinter2W");
}

extern "C" __declspec(dllexport) void SetPrinterDataW()
{
	my_debug("SetPrinterDataW");
}

extern "C" __declspec(dllexport) void GetPrinterW()
{
	my_debug("GetPrinterW");
}

extern "C" __declspec(dllexport) void EnumPrinterDataExW()
{
	my_debug("EnumPrinterDataExW");
}

extern "C" __declspec(dllexport) void SetPrinterDataExW()
{
	my_debug("SetPrinterDataExW");
}

extern "C" __declspec(dllexport) void DeletePrinterDataExW()
{
	my_debug("DeletePrinterDataExW");
}

extern "C" __declspec(dllexport) void DeletePrinterDataW()
{
	my_debug("DeletePrinterDataW");
}

extern "C" __declspec(dllexport) void SetJobW()
{
	my_debug("SetJobW");
}

extern "C" __declspec(dllexport) void FindClosePrinterChangeNotification()
{
	my_debug("FindClosePrinterChangeNotification");
}

extern "C" __declspec(dllexport) void FindFirstPrinterChangeNotification()
{
	my_debug("FindFirstPrinterChangeNotification");
}

extern "C" __declspec(dllexport) void  EnumPrintersW()
{
	my_debug("EnumPrintersW");
}

extern "C" __declspec(dllexport) void FreePrinterNotifyInfo()
{
	my_debug("FreePrinterNotifyInfo");
}

extern "C" __declspec(dllexport) void FindNextPrinterChangeNotification()
{
	my_debug("FindNextPrinterChangeNotification");
}

extern "C" __declspec(dllexport) void GetPrinterDriverW()
{
	my_debug("GetPrinterDriverW");
}

extern "C" __declspec(dllexport) void EnumJobsW()
{
	my_debug("EnumJobsW");
}

extern "C" __declspec(dllexport) void DeleteFormW()
{
	my_debug("DeleteFormW");
}

extern "C" __declspec(dllexport) void AddFormW()
{
	my_debug("AddFormW");
}

extern "C" __declspec(dllexport) void EnumFormsW()
{
	my_debug("EnumFormsW");
}
*/


BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
		my_debug("DLL_PROCESS_ATTACH");
		break;

    case DLL_THREAD_ATTACH:
		my_debug("DLL_THREAD_ATTACH");
		break;

    case DLL_THREAD_DETACH:
		my_debug("DLL_THREAD_DETACH");
		break;

	case DLL_PROCESS_DETACH:
		my_debug("DLL_PROCESS_DETACH");
		break;
    }
    return TRUE;
}

