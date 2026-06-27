using Microsoft.Win32;
using TakingMedications.Common;

namespace TakingMedications.Services;

/// <summary>
/// Управляет записью автозапуска приложения в реестре Windows
/// (HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run).
/// Порт Python med_background.py — autostart_create / autostart_remove.
/// </summary>
internal static class AutostartHelper
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName  = "TakingMedications";

    public static bool IsRegistered()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return k?.GetValue(ValueName) != null;
        }
        catch { return false; }
    }

    /// <summary>Прописывает exe в автозапуск Windows с флагом --start-hidden.</summary>
    public static void Register()
    {
        try
        {
            var exe = PathHelpers.ExePath;
            using var k = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            k?.SetValue(ValueName, $"\"{exe}\" --start-hidden");
        }
        catch { }
    }

    public static void Unregister()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            k?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { }
    }
}
