@echo off
echo Building WindowDiagnostic.exe...
"D:\VisualStudio\VC\Tools\MSVC\14.50.35717\bin\Hostx64\x64\cl.exe" /EHsc /std:c++17 /O2 /DUNICODE /D_UNICODE /Fe:WindowDiagnostic.exe WindowDiagnostic.cpp user32.lib psapi.lib
if %ERRORLEVEL% EQU 0 (
    echo Build successful!
    echo Run: WindowDiagnostic.exe
) else (
    echo Build failed!
)
