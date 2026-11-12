// SPDX-License-Identifier: GPL-3.0-only
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

/* A native x64 helper avoids 32-bit setup filesystem redirection and does
   not require .NET or the very VC runtime that it checks. Exit bits:
   1 = VC runtime unavailable; 2 = legacy DirectX unavailable; 4 = probe error. */
static int available(const wchar_t *name) {
    HMODULE module = LoadLibraryExW(name, NULL, LOAD_LIBRARY_SEARCH_SYSTEM32);
    if (!module) return 0;
    FreeLibrary(module);
    return 1;
}
int main(void) {
    int result = 0;
    if (!available(L"MSVCP140.dll") || !available(L"VCRUNTIME140.dll") ||
        !available(L"VCRUNTIME140_1.dll")) result |= 1;
    if (!available(L"D3DX9_43.dll")) result |= 2;
    return result;
}
