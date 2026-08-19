using System.IO;
using Microsoft.Win32;

namespace StayAwake.Core;

/// <summary>
/// Liga e desliga o início automático usando a chave Run do próprio usuário (HKCU).
/// Não exige administrador e não toca em nada do sistema.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "StayAwake";

    // Environment.ProcessPath funciona inclusive no exe de arquivo unico.
    public static string ExecutablePath =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "StayAwake.exe");

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string s && s.Contains("StayAwake", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return false;

            if (enabled)
                key.SetValue(ValueName, $"\"{ExecutablePath}\" --minimized");
            else if (key.GetValue(ValueName) is not null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
