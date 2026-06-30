using System;
using System.IO;

namespace TakingMedications.Common;

/// <summary>
/// Резолв путей данных пациента — **совместимо с Python v59**.
///
/// Python хранит данные так:
///   %APPDATA%\Приём лекарств\
///       default_profile.txt           ← short_name активного профиля
///       profiles\
///           &lt;short_name&gt;\
///               profile.json          ← карточка пациента
///               medications.json      ← список препаратов профиля
///               med_state_v41.json    ← state (имя зафиксировано на v41)
///
/// Этот класс возвращает те же пути, чтобы C# и Python работали с
/// **одними и теми же файлами**. Если профилей нет (новая установка
/// или dev) — fallback на exe-папку (single-profile legacy).
///
/// Принцип forward-compat: имя state-файла зафиксировано как
/// <see cref="StateFileName"/> = "med_state_v41.json" с v41
/// multi-profile в Python и не меняется при bump'ах приложения.
/// </summary>
internal static class AppPaths
{
    public const string AppName        = "Приём лекарств";
    public const string ProfilesDir    = "profiles";
    public const string DefaultFile    = "default_profile.txt";
    public const string StateFileName  = "med_state_v41.json";
    public const string MedsFileName   = "medications.json";
    public const string ProfileMeta    = "profile.json";

    /// <summary>%APPDATA%\Приём лекарств\ — корневая папка.</summary>
    public static string AppDataRoot
    {
        get
        {
            var appdata = Environment.GetEnvironmentVariable("APPDATA");
            if (string.IsNullOrEmpty(appdata))
            {
                // Fallback для Linux/macOS (не наш кейс, но для тестов).
                appdata = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config");
            }
            return Path.Combine(appdata, AppName);
        }
    }

    /// <summary>%APPDATA%\Приём лекарств\profiles\.</summary>
    public static string ProfilesRoot => Path.Combine(AppDataRoot, ProfilesDir);

    /// <summary>%APPDATA%\Приём лекарств\default_profile.txt.</summary>
    public static string DefaultProfilePointer => Path.Combine(AppDataRoot, DefaultFile);

    /// <summary>Имя активного профиля (из default_profile.txt) или null.</summary>
    public static string? GetDefaultProfileName()
    {
        try
        {
            if (!File.Exists(DefaultProfilePointer)) return null;
            var name = File.ReadAllText(DefaultProfilePointer).Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch (IOException)            { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>Папка профиля по short_name (без проверки существования).</summary>
    public static string GetProfileDir(string shortName)
        => Path.Combine(ProfilesRoot, shortName);

    /// <summary>
    /// Путь к state-файлу для использования сейчас. Логика:
    /// <list type="number">
    ///   <item>Если есть default_profile.txt и его папка профиля
    ///         существует — берём оттуда.</item>
    ///   <item>Иначе — fallback на exe-папку (single-profile legacy)
    ///         с тем же именем <see cref="StateFileName"/>.</item>
    /// </list>
    /// </summary>
    public static string ResolveStateFilePath()
    {
        var name = GetDefaultProfileName();
        if (!string.IsNullOrEmpty(name))
        {
            var profileDir = GetProfileDir(name);
            if (Directory.Exists(profileDir))
                return Path.Combine(profileDir, StateFileName);
        }
        // Fallback: рядом с exe (legacy single-profile).
        return Path.Combine(PathHelpers.BaseDir, StateFileName);
    }

    /// <summary>
    /// Путь к medications.json по тем же правилам, что и state-файл —
    /// чтобы оба файла лежали в одной папке (профиль или exe-fallback).
    /// </summary>
    public static string ResolveMedicationsFilePath()
    {
        var name = GetDefaultProfileName();
        if (!string.IsNullOrEmpty(name))
        {
            var profileDir = GetProfileDir(name);
            if (Directory.Exists(profileDir))
                return Path.Combine(profileDir, MedsFileName);
        }
        return Path.Combine(PathHelpers.BaseDir, MedsFileName);
    }

    /// <summary>
    /// Папка, в которой будут лежать state и medications. Возвращается
    /// для <c>StateStore</c> / <c>MedicationsRepository</c>, которые
    /// принимают baseDir в конструкторе.
    /// </summary>
    public static string ResolveDataDir()
    {
        var name = GetDefaultProfileName();
        if (!string.IsNullOrEmpty(name))
        {
            var profileDir = GetProfileDir(name);
            if (Directory.Exists(profileDir))
                return profileDir;
        }
        return PathHelpers.BaseDir;
    }

    /// <summary>
    /// Папка медицинских документов — **та же, что у Python**:
    /// <c>%USERPROFILE%\Documents\Приём лекарств\&lt;short_name&gt;\MedInfo</c>.
    /// Так C# и Python видят одни документы (раньше C# смотрел в AppData
    /// и не находил файлы Python). Если профиля нет — fallback в data-папку.
    /// </summary>
    public static string ResolveDocumentsDir()
    {
        var name = GetDefaultProfileName();
        if (!string.IsNullOrEmpty(name))
        {
            // ВАЖНО: как в Python — os.path.expanduser("~")\Documents, т.е.
            // %USERPROFILE%\Documents, а НЕ SpecialFolder.MyDocuments
            // (последний на этой машине перенаправлен в OneDrive\Документы).
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Documents", AppName, name, "MedInfo");
        }
        return Path.Combine(ResolveDataDir(), "MedInfo");
    }

    /// <summary>
    /// Папка для standalone-режима (без Python-приложения).
    /// %APPDATA%\TakingMedications\ — не пересекается с папкой Python.
    /// </summary>
    public static string StandaloneDataDir
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TakingMedications");

    /// <summary>
    /// Возвращает true, если данные Python-приложения присутствуют:
    /// существует папка profiles И в ней есть хотя бы один профиль,
    /// совпадающий с default_profile.txt. Именно это условие переключает
    /// приложение в режим JSON-совместимости.
    /// </summary>
    public static bool HasLinkedPythonProfile()
    {
        var name = GetDefaultProfileName();
        if (string.IsNullOrEmpty(name)) return false;
        return Directory.Exists(GetProfileDir(name));
    }

    /// <summary>
    /// One-shot миграция: если в data-папке есть legacy
    /// <c>med_state_v1.json</c> (от C# v1), а нового
    /// <c>med_state_v41.json</c> нет — переименовываем. Один раз
    /// при первом запуске v1.1+. Безопасно вызывать повторно.
    /// </summary>
    public static void MigrateLegacyV1StateIfNeeded(string dataDir)
    {
        try
        {
            var legacy = Path.Combine(dataDir, "med_state_v1.json");
            var canonical = Path.Combine(dataDir, StateFileName);
            if (File.Exists(legacy) && !File.Exists(canonical))
            {
                File.Copy(legacy, canonical, overwrite: false);
                // Не удаляем legacy — оставляем как backup.
                // Удалить можно вручную, когда автор C# убедится, что
                // v41 формат работает у пациента.
            }
        }
        catch (IOException)            { /* silent */ }
        catch (UnauthorizedAccessException) { /* silent */ }
    }
}
