#pragma once
#include <Windows.h>
#include <wrl/client.h>

using Microsoft::WRL::ComPtr;

namespace CrystalFrame {

// Accent state for SetWindowCompositionAttribute
enum ACCENT_STATE {
    ACCENT_DISABLED = 0,
    ACCENT_ENABLE_GRADIENT = 1,
    ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
    ACCENT_ENABLE_BLURBEHIND = 3,
    ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
    ACCENT_ENABLE_HOSTBACKDROP = 5,
    ACCENT_INVALID_STATE = 6
};

struct ACCENT_POLICY {
    ACCENT_STATE AccentState;
    DWORD AccentFlags;
    DWORD GradientColor;  // ABGR format
    DWORD AnimationId;
};

struct WINDOWCOMPOSITIONATTRIBDATA {
    DWORD Attrib;
    PVOID pvData;
    SIZE_T cbData;
};

// Window Composition Attribute constants
constexpr DWORD WCA_ACCENT_POLICY = 19;

// Function pointer for undocumented API
typedef BOOL(WINAPI* pfnSetWindowCompositionAttribute)(HWND, WINDOWCOMPOSITIONATTRIBDATA*);

class Renderer {
public:
    Renderer();
    ~Renderer();

    bool Initialize();
    void Shutdown();

    // Set target windows
    void SetTaskbarWindow(HWND hwnd);
    void SetStartWindow(HWND hwnd);

    // Opacity control (0 = fully transparent, 100 = opaque)
    void SetTaskbarOpacity(int opacity);
    void SetStartOpacity(int opacity);

    // Color control (RGB 0-255)
    void SetTaskbarColor(int r, int g, int b);

    // Enable/disable transparency
    void SetTaskbarEnabled(bool enabled);
    void SetStartEnabled(bool enabled);

    // Reapply transparency (call periodically to maintain effect)
    void RefreshTransparency();

private:
    pfnSetWindowCompositionAttribute m_setWindowCompositionAttribute = nullptr;

    HWND m_hwndTaskbar = nullptr;
    HWND m_hwndStart = nullptr;

    int m_taskbarOpacity = 75;
    int m_startOpacity = 50;
    bool m_taskbarEnabled = true;
    bool m_startEnabled = true;

    int m_taskbarColorR = 0;
    int m_taskbarColorG = 0;
    int m_taskbarColorB = 0;

    void ApplyTransparency(HWND hwnd, int opacity, bool enabled);
    void ApplyTransparencyWithColor(HWND hwnd, int opacity, bool enabled, int r, int g, int b);
    void RestoreWindow(HWND hwnd);
};

} // namespace CrystalFrame
