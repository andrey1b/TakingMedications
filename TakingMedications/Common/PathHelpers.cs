using System;
using System.IO;

namespace TakingMedications.Common;

internal static class PathHelpers
{
    /// <summary>
    /// Папка рядом с exe (или с .csproj-сборкой при отладке).
    /// Сюда пишем medications.json и med_state_v1.json.
    /// </summary>
    public static string BaseDir
        => Path.GetDirectoryName(AppContext.BaseDirectory) ?? Environment.CurrentDirectory;

    /// <summary>Полный путь к исполняемому файлу (для записи автозапуска).</summary>
    public static string ExePath
        => System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
           ?? Path.Combine(BaseDir, "TakingMedications.exe");

    public static string TodayIso()
        => DateTime.Now.ToString("yyyy-MM-dd");
}
