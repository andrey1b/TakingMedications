using System;
using System.IO;
using TakingMedications.Common;
using TakingMedications.Models;

namespace TakingMedications.Services;

/// <summary>
/// Загрузка и сохранение AppState в <c>med_state_v41.json</c>.
/// Атомарная запись: пишем в .tmp, потом File.Replace.
///
/// ── СОВМЕСТИМОСТЬ С PYTHON v59 ──────────────────────────────────────
/// Имя файла зафиксировано на <c>med_state_v41.json</c> (а не v1.json
/// как было раньше) — это тот же файл, что использует Python с v41.
/// Multi-profile логику (выбор активного профиля из
/// <c>%APPDATA%\Приём лекарств\default_profile.txt</c>) реализует
/// <see cref="AppPaths"/>; этот класс просто принимает baseDir
/// и пишет туда имя файла из <see cref="AppPaths.StateFileName"/>.
///
/// При первом запуске после смены имени делаем one-shot миграцию
/// legacy <c>med_state_v1.json</c> → <c>med_state_v41.json</c>
/// (см. <see cref="AppPaths.MigrateLegacyV1StateIfNeeded"/>).
/// </summary>
public class StateStore : IStateRepository
{
    private readonly string _path;

    public StateStore(string baseDir)
    {
        // One-shot миграция legacy v1 → v41 (безопасно при повторных
        // вызовах: мигрирует только если v1 есть и v41 нет).
        AppPaths.MigrateLegacyV1StateIfNeeded(baseDir);

        _path = Path.Combine(baseDir, AppPaths.StateFileName);
    }

    public AppState Load()
    {
        if (!File.Exists(_path)) return new AppState();
        try
        {
            return AppState.FromJson(File.ReadAllText(_path));
        }
        catch (Exception)
        {
            return new AppState();
        }
    }

    public void Save(AppState state)
    {
        MedicationsRepository.AtomicWrite(_path, state.ToJson());
    }
}
