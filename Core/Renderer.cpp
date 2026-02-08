#include "Renderer.h"
#include "Diagnostics.h"
#include <algorithm>

namespace CrystalFrame {

Renderer::Renderer() {
}

Renderer::~Renderer() {
    Shutdown();
}

bool Renderer::Initialize() {
    // Load SetWindowCompositionAttribute from user32.dll
    HMODULE hUser32 = GetModuleHandleW(L"user32.dll");
    if (!hUser32) {
        hUser32 = LoadLibraryW(L"user32.dll");
    }

    if (!hUser32) {
        CF_LOG(Error, "Failed to load user32.dll");
        return false;
    }

    m_setWindowCompositionAttribute = reinterpret_cast<pfnSetWindowCompositionAttribute>(
        GetProcAddress(hUser32, "SetWindowCompositionAttribute")
    );

    if (!m_setWindowCompositionAttribute) {
        CF_LOG(Error, "SetWindowCompositionAttribute not found in user32.dll");
        return false;
    }

    CF_LOG(Info, "Renderer initialized (SetWindowCompositionAttribute ready)");
    return true;
}

void Renderer::Shutdown() {
    // Restore original window states
    if (m_hwndTaskbar) {
        RestoreWindow(m_hwndTaskbar);
    }
    if (m_hwndStart) {
        RestoreWindow(m_hwndStart);
    }

    CF_LOG(Info, "Renderer shutdown");
}

void Renderer::SetTaskbarWindow(HWND hwnd) {
    m_hwndTaskbar = hwnd;
    if (hwnd) {
        ApplyTransparency(hwnd, m_taskbarOpacity, m_taskbarEnabled);
        CF_LOG(Info, "Taskbar window set: 0x" << std::hex << reinterpret_cast<uintptr_t>(hwnd));
    }
}

void Renderer::SetStartWindow(HWND hwnd) {
    m_hwndStart = hwnd;
    if (hwnd) {
        ApplyTransparency(hwnd, m_startOpacity, m_startEnabled);
        CF_LOG(Info, "Start window set: 0x" << std::hex << reinterpret_cast<uintptr_t>(hwnd));
    }
}

void Renderer::SetTaskbarOpacity(int opacity) {
    opacity = std::clamp(opacity, 0, 100);
    m_taskbarOpacity = opacity;

    if (m_hwndTaskbar && m_taskbarEnabled) {
        ApplyTransparency(m_hwndTaskbar, opacity, true);
        CF_LOG(Debug, "Taskbar opacity set to " << opacity << "%");
    }
}

void Renderer::SetStartOpacity(int opacity) {
    opacity = std::clamp(opacity, 0, 100);
    m_startOpacity = opacity;

    CF_LOG(Info, "SetStartOpacity called: opacity=" << opacity
                 << ", m_hwndStart=" << m_hwndStart
                 << ", m_startEnabled=" << m_startEnabled);

    if (m_hwndStart && m_startEnabled) {
        // Verify window is still valid
        if (!IsWindow(m_hwndStart)) {
            CF_LOG(Warning, "Start window handle is no longer valid!");
            m_hwndStart = nullptr;
            return;
        }

        CF_LOG(Info, "Applying transparency to Start Menu window");
        ApplyTransparency(m_hwndStart, opacity, true);
        CF_LOG(Info, "Start opacity set to " << opacity << "%");
    } else {
        if (!m_hwndStart) {
            CF_LOG(Warning, "Start window handle is NULL - cannot apply opacity");
        }
        if (!m_startEnabled) {
            CF_LOG(Info, "Start transparency is disabled");
        }
    }
}

void Renderer::SetTaskbarColor(int r, int g, int b) {
    m_taskbarColorR = std::clamp(r, 0, 255);
    m_taskbarColorG = std::clamp(g, 0, 255);
    m_taskbarColorB = std::clamp(b, 0, 255);

    if (m_hwndTaskbar && m_taskbarEnabled) {
        ApplyTransparencyWithColor(m_hwndTaskbar, m_taskbarOpacity, true, m_taskbarColorR, m_taskbarColorG, m_taskbarColorB);
    }
}

void Renderer::SetTaskbarEnabled(bool enabled) {
    m_taskbarEnabled = enabled;

    if (m_hwndTaskbar) {
        if (enabled) {
            ApplyTransparency(m_hwndTaskbar, m_taskbarOpacity, true);
        } else {
            RestoreWindow(m_hwndTaskbar);
        }
        CF_LOG(Info, "Taskbar transparency " << (enabled ? "enabled" : "disabled"));
    }
}

void Renderer::SetStartEnabled(bool enabled) {
    m_startEnabled = enabled;

    if (m_hwndStart) {
        if (enabled) {
            ApplyTransparency(m_hwndStart, m_startOpacity, true);
        } else {
            RestoreWindow(m_hwndStart);
        }
        CF_LOG(Info, "Start transparency " << (enabled ? "enabled" : "disabled"));
    }
}

void Renderer::ApplyTransparency(HWND hwnd, int opacity, bool enabled) {
    if (hwnd == m_hwndTaskbar) {
        ApplyTransparencyWithColor(hwnd, opacity, enabled, m_taskbarColorR, m_taskbarColorG, m_taskbarColorB);
    } else {
        ApplyTransparencyWithColor(hwnd, opacity, enabled, 0, 0, 0);
    }
}

void Renderer::ApplyTransparencyWithColor(HWND hwnd, int opacity, bool enabled, int r, int g, int b) {
    // Check which window type this is
    bool isStartMenu = (hwnd == m_hwndStart);
    const char* windowType = isStartMenu ? "START MENU" : "TASKBAR";

    CF_LOG(Info, "[" << windowType << "] ApplyTransparencyWithColor called: HWND=0x" << std::hex << reinterpret_cast<uintptr_t>(hwnd) << std::dec
                 << ", opacity=" << opacity << ", enabled=" << enabled << ", RGB=(" << r << "," << g << "," << b << ")");

    if (!hwnd || !IsWindow(hwnd) || !m_setWindowCompositionAttribute) {
        CF_LOG(Warning, "[" << windowType << "] ApplyTransparencyWithColor early return: hwnd=" << hwnd
                      << ", IsWindow=" << (hwnd ? IsWindow(hwnd) : 0)
                      << ", m_setWindowCompositionAttribute=" << (m_setWindowCompositionAttribute ? "valid" : "NULL"));
        return;
    }

    ACCENT_POLICY accent = {};

    if (enabled && opacity > 0) {
        // Use ACCENT_ENABLE_TRANSPARENTGRADIENT for true transparency
        accent.AccentState = ACCENT_ENABLE_TRANSPARENTGRADIENT;

        // Alpha from opacity slider (0% = opaque/255, 100% = transparent/0)
        BYTE alpha = static_cast<BYTE>(((100 - opacity) * 255) / 100);

        // GradientColor format: ABGR (Alpha, Blue, Green, Red)
        // Combine: opacity slider (alpha channel) + RGB sliders (color)
        DWORD gradientColor = (alpha << 24) | (b << 16) | (g << 8) | r;
        accent.GradientColor = gradientColor;
        accent.AccentFlags = 2;

        CF_LOG(Info, "[" << windowType << "] Applying TRANSPARENTGRADIENT: opacity=" << opacity
                     << "%, alpha=" << (int)alpha
                     << ", RGB=(" << r << "," << g << "," << b << ")"
                     << ", GradientColor=0x" << std::hex << gradientColor << std::dec);
    } else {
        // Disable transparency effect - normal opaque taskbar
        accent.AccentState = ACCENT_DISABLED;
        accent.GradientColor = 0;
        accent.AccentFlags = 0;

        CF_LOG(Info, "[" << windowType << "] Disabling transparency: opacity=" << opacity << "%, enabled=" << enabled);
    }

    WINDOWCOMPOSITIONATTRIBDATA data = {};
    data.Attrib = WCA_ACCENT_POLICY;
    data.pvData = &accent;
    data.cbData = sizeof(accent);

    BOOL result = m_setWindowCompositionAttribute(hwnd, &data);
    CF_LOG(Info, "SetWindowCompositionAttribute result: " << result
                 << " for HWND 0x" << std::hex << reinterpret_cast<uintptr_t>(hwnd) << std::dec);
}

void Renderer::RestoreWindow(HWND hwnd) {
    CF_LOG(Info, "RestoreWindow called for HWND 0x" << std::hex << reinterpret_cast<uintptr_t>(hwnd) << std::dec);

    if (!hwnd || !IsWindow(hwnd) || !m_setWindowCompositionAttribute) {
        if (hwnd && !IsWindow(hwnd)) {
            CF_LOG(Warning, "Window handle is no longer valid, cannot restore");
        }
        return;
    }

    ACCENT_POLICY accent = {};
    accent.AccentState = ACCENT_DISABLED;
    accent.AccentFlags = 0;
    accent.GradientColor = 0;
    accent.AnimationId = 0;

    WINDOWCOMPOSITIONATTRIBDATA data = {};
    data.Attrib = WCA_ACCENT_POLICY;
    data.pvData = &accent;
    data.cbData = sizeof(accent);

    BOOL result = m_setWindowCompositionAttribute(hwnd, &data);
    CF_LOG(Info, "RestoreWindow SetWindowCompositionAttribute result: " << result
                 << " for HWND 0x" << std::hex << reinterpret_cast<uintptr_t>(hwnd) << std::dec);
}

void Renderer::RefreshTransparency() {
    // Reapply transparency to maintain the effect
    // Windows can reset it on certain events
    if (m_hwndTaskbar && m_taskbarEnabled) {
        ApplyTransparency(m_hwndTaskbar, m_taskbarOpacity, true);
    }
    // Skip refreshing Start menu - Windows handles it and refreshing causes flicker
}

} // namespace CrystalFrame
