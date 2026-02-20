# Plan: Start Menu Fixes

## Cerințe

1. **Poziționare corectă** – Start Menu să apară ancorat la butonul Windows, indiferent de poziția taskbar-ului (jos, sus, stânga, dreapta).
2. **Meniuri funcționale** – Search Box și zona User/Avatar să răspundă la click.
3. **Autostart → tray** – Dacă aplicația este setată să pornească cu Windows, la pornire se va ascunde în System tray (fără fereastră).

---

## Task 1 – Poziționare Start Menu ancorat la butonul Start ✅ DONE

### Problema rezolvată
`Show(int x, int y)` – meniu-ul se centra mereu orizontal.
Acum se ancorează la butonul Start indiferent de orientarea taskbar-ului.

### Fișier modificat
`Core/StartMenuWindow.cpp` – funcția `Show()` (liniile 125–155)

### Logică implementată (pas cu pas)

**Pas 1 – Obține RECT taskbar**
```cpp
HWND taskbar = FindWindowW(L"Shell_TrayWnd", nullptr);
RECT tbRect = {};
GetWindowRect(taskbar, &tbRect);
```

**Pas 2 – Detectează orientarea taskbar**
- **Jos**: `tbRect.bottom >= screenH - 4`
- **Sus**: `tbRect.top <= 4 && tbRect.bottom < screenH / 2`
- **Stânga**: `tbRect.left <= 4 && tbRect.right < screenW / 2`
- **Dreapta**: `tbRect.right >= screenW - 4`

**Pas 3 – Găsește RECT Start button**
```cpp
HWND startBtn = FindWindowExW(taskbar, nullptr, L"Start", nullptr);
if (!startBtn) startBtn = FindWindowExW(taskbar, nullptr, L"TrayButton", nullptr);
```

**Pas 4 – Calculează poziția în funcție de orientare**
| Orientare    | menuX                                         | menuY                         |
|--------------|-----------------------------------------------|-------------------------------|
| Taskbar jos  | `clamp(sbRect.left, 0, screenW - WIDTH)`      | `tbRect.top - HEIGHT - 1`     |
| Taskbar sus  | `clamp(sbRect.left, 0, screenW - WIDTH)`      | `tbRect.bottom + 1`           |
| Taskbar stânga | `tbRect.right + 1`                          | `clamp(sbRect.top, 0, screenH - HEIGHT)` |
| Taskbar dreapta | `tbRect.left - WIDTH - 1`                  | `clamp(sbRect.top, 0, screenH - HEIGHT)` |
| Fallback     | `(screenW - WIDTH) / 2`                       | `screenH - HEIGHT - 48`       |

**Pas 5 – Clamp final** – asigură că meniu-ul nu iese din ecran.

---

## Task 2 – Search Box funcțional (click → Windows Search) ✅ DONE

### Problema rezolvată
`PaintSearchBox()` desenează box-ul, dar click-ul nu era interceptat.
Acum click pe search box deschide Windows Search (`ms-search:`).

### Implementare
- Hit test pe zona `[MARGIN, SEARCH_Y, cr.right-MARGIN, SEARCH_Y+SEARCH_H]`
- `WM_LBUTTONDOWN`: `ShellExecuteW(NULL, L"open", L"ms-search:", NULL, NULL, SW_SHOW)`
- Hover state cu redesenare la `WM_MOUSEMOVE`

---

## Task 3 – Zona User/Avatar funcțională (click → Setări cont) ✅ DONE

### Problema rezolvată
Avatar circle + "User" text erau desenate dar fără hit test sau acțiune.
Acum click pe zona user deschide `ms-settings:accounts`.

### Implementare
- Hit test pe zona avatar + text (stânga bottom bar)
- `WM_LBUTTONDOWN`: `ShellExecuteW(NULL, L"open", L"ms-settings:accounts", NULL, NULL, SW_SHOW)`
- Hover highlight subtil, același pattern ca pinned items

---

## Task 4 – Autostart → porneşte hidden în System Tray ✅ DONE

### Problema rezolvată
La pornirea Windows (autostart prin registry), aplicația deschidea fereastra principală
în loc să se ascundă silențios în System Tray.

### Fișiere modificate

**`Dashboard/StartupManager.cs`** – linia `SetEnabled(true)`:
```csharp
// Înainte:
key.SetValue(AppName, $"\"{path}\"");
// După:
key.SetValue(AppName, $"\"{path}\" /autostart");
```
Argumentul `/autostart` este detectat la lansare și declanșează modul silențios.

**`Dashboard/App.xaml.cs`** – `OnLaunched()`:
```csharp
bool startHidden = args.Arguments.Contains("/autostart", StringComparison.OrdinalIgnoreCase)
                || Environment.GetCommandLineArgs().Any(
                       a => a.Equals("/autostart", StringComparison.OrdinalIgnoreCase));
_window = new MainWindow(startHidden);
_window.Activate();
```

**`Dashboard/MainWindow.xaml.cs`** – constructor:
```csharp
public MainWindow(bool startHidden = false)
{
    // ... init existent ...

    // When launched at Windows startup, stay hidden in tray until user opens manually
    if (startHidden)
        DispatcherQueue.TryEnqueue(() => _appWindow.Hide());

    _ = InitializeAsync();
}
```

### Comportament rezultat
| Mod lansare | Comportament |
|-------------|--------------|
| Normal (dublu-click pe .exe) | Deschide fereastra principală ca înainte |
| Autostart (registry Run key) | Pornește silențios, icoană în System Tray |
| Dublu-click icoană tray | Deschide fereastra principală |
| Click dreapta tray → "Open" | Deschide fereastra principală |
| Click dreapta tray → "Exit" | Oprește Core + iese complet |

---

## Ordine de implementare

1. ✅ Task 1 (poziționare) – izolată în `Show()`
2. ✅ Task 2 (search box) – hover + click
3. ✅ Task 3 (user area) – hover + click
4. ✅ Task 4 (autostart tray) – `/autostart` flag + `_appWindow.Hide()`

## Următori pași posibili

- **Multi-monitor** – overlay pe display-uri non-primare
- **Global hotkey** – toggle overlay-uri fără Dashboard
- **Color presets** – teme predefinite (Aero Glass, Dark, etc.)
- **Auto-update check** – notificare la nouă versiune GitHub
- **Persist JSON fix** – trailing-comma în `SaveCustomNames()` ✅ rezolvat

## Ce NU facem (pentru a evita bug-uri)
- Nu modificăm hook-urile (StartMenuHook.cpp) – funcționează corect
- Nu modificăm sistemul de rendering/transparență
- Nu schimbăm dimensiunile ferestrei (WIDTH/HEIGHT)
- Nu adăugăm input text real în search box (risc ridicat, complex)
