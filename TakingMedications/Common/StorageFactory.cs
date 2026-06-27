using System.IO;
using TakingMedications.Services;

namespace TakingMedications.Common;

public enum StorageMode { Json, Sqlite }

/// <summary>
/// Выбирает режим хранения при старте приложения:
/// — Json    : если обнаружены данные Python-приложения (%APPDATA%\Приём лекарств\profiles\)
/// — Sqlite  : если Python-данных нет (новый пользователь, standalone-запуск)
/// </summary>
public static class StorageFactory
{
    public record Result(
        IStateRepository       StateRepo,
        IMedicationsRepository MedsRepo,
        string                 DataDir,
        StorageMode            Mode);

    public static Result Create()
    {
        if (AppPaths.HasLinkedPythonProfile())
        {
            var dataDir = AppPaths.ResolveDataDir();
            return new Result(
                new StateStore(dataDir),
                new MedicationsRepository(dataDir),
                dataDir,
                StorageMode.Json);
        }

        var standaloneDir = AppPaths.StandaloneDataDir;
        Directory.CreateDirectory(standaloneDir);
        var repo = new SqliteRepository(Path.Combine(standaloneDir, "medications.db"));
        return new Result(repo, repo, standaloneDir, StorageMode.Sqlite);
    }
}
