# Plan: Start Menu Fixes

## Cerințe

1. **Poziționare corectă** – Start Menu să apară ancorat la butonul Windows, indiferent de poziția taskbar-ului (jos, sus, stânga, dreapta).
2. **Meniuri funcționale** – Search Box și zona User/Avatar să răspundă la click.

---

## Task 1 – Poziționare Start Menu ancorat la butonul Start

### Problema actuală
`Show(int /*x*/, int /*y*/)` – parametrii sunt ignorați (comentați).
Meniu-ul se centrează mereu orizontal: `menuX = (screenW - WIDTH) / 2`.
Rezultat: taskbar pe stânga → meniu apare la mijlocul ecranului, nu lângă iconița Windows.

### Fișier modificat
`Core/StartMenuWindow.cpp` – funcția `Show()` (liniile 125–155)

### Logică nouă (pas cu pas)

**Pas 1 – Obține RECT taskbar**
```cpp
HWND taskbar = FindWindowW(L"Shell_TrayWnd", nullptr);
RECT tbRect = {};
GetWindowRect(taskbar, &tbRect);
```

**Pas 2 – Detectează orientarea taskbar**
Comparăm dimensiunile RECT cu dimensiunile ecranului:
- **Jos**: `tbRect.bottom >= screenH - 4`
- **Sus**: `tbRect.top <= 4 && tbRect.bottom < screenH / 2`
- **Stânga**: `tbRect.left <= 4 && tbRect.right < screenW / 2`
- **Dreapta**: `tbRect.right >= screenW - 4`

**Pas 3 – Găsește RECT Start button direct în Show()**
```cpp
HWND startBtn = FindWindowExW(taskbar, nullptr, L"Start", nullptr);
if (!startBtn) startBtn = FindWindowExW(taskbar, nullptr, L"TrayButton", nullptr);
RECT sbRect = {};
GetWindowRect(startBtn, &sbRect);
// Fallback la parametrii x, y dacă start button nu e găsit
```

**Pas 4 – Calculează poziția în funcție de orientare**
| Orientare    | menuX                                         | menuY                         |
|--------------|-----------------------------------------------|-------------------------------|
| Taskbar jos  | `clamp(sbRect.left, 0, screenW - WIDTH)`      | `tbRect.top - HEIGHT - 1`     |
| Taskbar sus  | `clamp(sbRect.left, 0, screenW - WIDTH)`      | `tbRect.bottom + 1`           |
| Taskbar stânga | `tbRect.right + 1`                          | `clamp(sbRect.top, 0, screenH - HEIGHT)` |
| Taskbar dreapta | `tbRect.left - WIDTH - 1`                  | `clamp(sbRect.top, 0, screenH - HEIGHT)` |
| Fallback     | `(screenW - WIDTH) / 2` (comportament actual) | `screenH - HEIGHT - 48`       |

**Pas 5 – Clamp final** – asigură că meniu-ul nu iese din ecran pe niciun ax.

### Risc
Scăzut – modificare izolată în `Show()`. Nu afectează rendering, hook-urile sau alte funcții.

---

## Task 2 – Search Box funcțional (click → Windows Search)

### Problema actuală
`PaintSearchBox()` desenează box-ul, dar `HandleMessage()` nu verifică click-uri pe zona search box.

### Fișiere modificate
- `Core/StartMenuWindow.h` – adaugă `m_hoveredSearch` (bool)
- `Core/StartMenuWindow.cpp`:
  - Adaugă `IsOverSearchBox(POINT pt)` – hit test pe zona `[MARGIN, SEARCH_Y, cr.right-MARGIN, SEARCH_Y+SEARCH_H]`
  - `PaintSearchBox()` – schimbă culoarea fundalului la hover (același pattern ca power button)
  - `HandleMessage()` – `WM_LBUTTONDOWN`: adaugă check pentru search box → `ShellExecuteW(L"ms-search:")`
  - `HandleMessage()` – `WM_MOUSEMOVE`: adaugă hover state pentru search box → `InvalidateRect`

### Comandă deschidere Windows Search
```cpp
ShellExecuteW(NULL, L"open", L"ms-search:", NULL, NULL, SW_SHOW);
```

### Risc
Scăzut – pattern identic cu power button (deja existent). Adăugare, nu modificare de logică existentă.

---

## Task 3 – Zona User/Avatar funcțională (click → Setări cont)

### Problema actuală
Avatar circle + "User" text sunt desenate în `PaintBottomBar()`, dar nu au hit test sau acțiune.

### Fișiere modificate
- `Core/StartMenuWindow.h` – adaugă `m_hoveredUser` (bool)
- `Core/StartMenuWindow.cpp`:
  - Adaugă `IsOverUserArea(POINT pt)` – hit test pe zona avatar + text (stânga bottom bar, ~[MARGIN, BOTTOM_BAR_Y, WIDTH/2, HEIGHT])
  - `PaintBottomBar()` – adaugă hover highlight pe zona user (dreptunghi subtil, același pattern ca hover pe pinned items)
  - `HandleMessage()` – `WM_LBUTTONDOWN`: adaugă check → `ShellExecuteW(L"ms-settings:accounts")`
  - `HandleMessage()` – `WM_MOUSEMOVE`: adaugă hover state → `InvalidateRect`

### Risc
Scăzut – pattern identic cu celelalte elemente interactive. Adăugare, nu modificare.

---

## Ordine de implementare

1. Task 1 (poziționare) – cea mai importantă, izolată în `Show()`
2. Task 2 (search box) – adăugare hover + click, urmând pattern existent
3. Task 3 (user area) – adăugare hover + click, urmând pattern existent

## Ce NU facem (pentru a evita bug-uri)
- Nu modificăm hook-urile (StartMenuHook.cpp) – funcționează corect
- Nu modificăm sistemul de rendering/transparență
- Nu schimbăm dimensiunile ferestrei (WIDTH/HEIGHT)
- Nu adăugăm input text real în search box (risc ridicat, complex)
- Nu atingem ConfigManager sau Dashboard
