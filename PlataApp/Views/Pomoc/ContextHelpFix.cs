using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PlataApp.Views.Pomoc;

/// <summary>
/// Prozori sa ResizeMode="NoResize" automatski dobijaju "?" dugme za kontekstualnu pomoć u naslovnoj traci (Win32 WS_EX_CONTEXTHELP),
/// zbog čega F1 šalje SC_CONTEXTHELP sistemsku komandu umesto običnog KeyDown događaja — pa Window_KeyDown F1 handler nikad ne okine.
/// Ova metoda uklanja to dugme kako bi F1 ponovo radio kao obična prečica.
/// </summary>
public static class ContextHelpFix
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_CONTEXTHELP = 0x00000400;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public static void UkloniDugmeZaPomoc(Window window)
    {
        window.SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_CONTEXTHELP);
        };
    }
}
