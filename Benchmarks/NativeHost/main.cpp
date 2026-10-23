// Isolated feasibility specimen only. No product migration or <20 MB claim.
// Build on Windows: cl /std:c++17 /EHsc main.cpp user32.lib gdi32.lib d2d1.lib dwrite.lib /link /SUBSYSTEM:WINDOWS
#include <windows.h>
#include <d2d1.h>
#include <dwrite.h>
#pragma comment(lib, "d2d1.lib")
#pragma comment(lib, "dwrite.lib")
ID2D1Factory* factory{}; ID2D1HwndRenderTarget* target{}; ID2D1SolidColorBrush* ink{};
IDWriteFactory* textFactory{}; IDWriteTextFormat* format{};
LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) {
    if(message == WM_PAINT) {
        PAINTSTRUCT paint; BeginPaint(window, &paint);
        if(!target) {
            RECT rect; GetClientRect(window, &rect);
            if(SUCCEEDED(factory->CreateHwndRenderTarget(D2D1::RenderTargetProperties(), D2D1::HwndRenderTargetProperties(window, D2D1::SizeU(rect.right, rect.bottom)), &target)))
                target->CreateSolidColorBrush(D2D1::ColorF(0.94f, 0.96f, 0.98f), &ink);
        }
        if(target && ink) {
            target->BeginDraw(); target->Clear(D2D1::ColorF(0.09f,0.12f,0.18f));
            const wchar_t text[] = L"Notification Island - native feasibility";
            target->DrawText(text, ARRAYSIZE(text)-1, format, D2D1::RectF(18,12,440,46), ink);
            if(target->EndDraw() == D2DERR_RECREATE_TARGET) { ink->Release(); ink=nullptr; target->Release(); target=nullptr; }
        }
        EndPaint(window, &paint); return 0;
    }
    if(message == WM_DESTROY) { PostQuitMessage(0); return 0; }
    return DefWindowProcW(window, message, wparam, lparam);
}
int WINAPI wWinMain(HINSTANCE instance,HINSTANCE,LPWSTR,int show) {
    if(FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, &factory))) return 1;
    if(FAILED(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), reinterpret_cast<IUnknown**>(&textFactory)))) return 2;
    textFactory->CreateTextFormat(L"Segoe UI",nullptr,DWRITE_FONT_WEIGHT_NORMAL,DWRITE_FONT_STYLE_NORMAL,DWRITE_FONT_STRETCH_NORMAL,13,L"tr-TR",&format);
    WNDCLASSW wc{}; wc.lpfnWndProc=WindowProc; wc.hInstance=instance; wc.lpszClassName=L"NI.Native.Feasibility"; RegisterClassW(&wc);
    HWND window=CreateWindowExW(WS_EX_TOOLWINDOW,wc.lpszClassName,L"NI native feasibility",WS_POPUP,400,12,460,48,nullptr,nullptr,instance,nullptr);
    if(!window || !format) return 3;
    HRGN region=CreateRoundRectRgn(0,0,460,48,48,48); if(!SetWindowRgn(window,region,TRUE)) DeleteObject(region);
    ShowWindow(window,show); MSG message; while(GetMessageW(&message,nullptr,0,0)>0) { TranslateMessage(&message); DispatchMessageW(&message); }
    if(ink) ink->Release(); if(target) target->Release(); if(format) format->Release(); if(textFactory) textFactory->Release(); factory->Release(); return 0;
}
