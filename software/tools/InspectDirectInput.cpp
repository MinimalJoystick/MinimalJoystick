#define DIRECTINPUT_VERSION 0x0800
#include <windows.h>
#include <dinput.h>
#include <stdio.h>
#include <wchar.h>
static int matches = 0;
static BOOL CALLBACK Device(const DIDEVICEINSTANCEW* info, VOID*) {
    WCHAR id[64]; StringFromGUID2(info->guidInstance, id, 64);
    wprintf(L"Instance: %s\nProduct: %s\nGUID: %s\n", info->tszInstanceName, info->tszProductName, id);
    if (wcscmp(info->tszProductName, L"MinimalJoystick") == 0) ++matches;
    return DIENUM_CONTINUE;
}
int wmain() {
    IDirectInput8W* input = nullptr;
    HRESULT result = DirectInput8Create(GetModuleHandleW(nullptr), DIRECTINPUT_VERSION, IID_IDirectInput8W, (void**)&input, nullptr);
    if (FAILED(result)) { printf("DirectInput8Create failed: %08lX\n", (unsigned long)result); return 1; }
    result = input->EnumDevices(DI8DEVCLASS_GAMECTRL, Device, nullptr, DIEDFL_ATTACHEDONLY);
    input->Release();
    if (FAILED(result)) return 1;
    printf("%s: DirectInput sees %d controller(s) with product name MinimalJoystick.\n", matches == 1 ? "PASS" : "FAIL", matches);
    return matches == 1 ? 0 : 1;
}
