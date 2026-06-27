using System;
using System.Collections.Generic;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications.Common;

/// <summary>
/// Контекст уровня приложения: shared mutable state. Все UserControl-вкладки
/// получают один и тот же экземпляр и подписываются на <see cref="DataChanged"/>.
///
/// ── РЕЖИМЫ ХРАНЕНИЯ ─────────────────────────────────────────────────
/// Json   : Python-приложение установлено и есть активный профиль в
///          %APPDATA%\Приём лекарств\  — C# читает те же JSON-файлы.
/// Sqlite : Python-данных нет (новый пользователь / standalone) —
///          данные хранятся в %APPDATA%\TakingMedications\medications.db.
/// Переключение происходит автоматически через <see cref="StorageFactory"/>.
///
/// Для тестов или явного указания папки используется перегрузка
/// конструктора с явным <c>baseDir</c> — всегда JSON-режим.
/// </summary>
public class MedAppContext
{
    public List<MedicationSection> Sections { get; private set; } = new();
    public AppState State { get; private set; } = new();

    public IMedicationsRepository MedsRepo { get; }
    public IStateRepository StateStore { get; }

    /// <summary>Активный data-dir: папка профиля или StandaloneDataDir.</summary>
    public string DataDir { get; }

    /// <summary>Json или Sqlite — определяется при старте приложения.</summary>
    public StorageMode StorageMode { get; }

    /// <summary>Любая мутация, которая может потребовать перерисовки других вкладок.</summary>
    public event Action? DataChanged;

    /// <summary>
    /// Default-конструктор: автоматически выбирает режим хранения через
    /// <see cref="StorageFactory"/> (Json если Python-данные есть, иначе Sqlite).
    /// </summary>
    public MedAppContext()
    {
        var f      = StorageFactory.Create();
        MedsRepo   = f.MedsRepo;
        StateStore = f.StateRepo;
        DataDir    = f.DataDir;
        StorageMode = f.Mode;
        Reload();
    }

    /// <summary>
    /// Явный baseDir — всегда JSON-режим (для тестов и dev-сценариев).
    /// </summary>
    public MedAppContext(string baseDir)
    {
        MedsRepo    = new MedicationsRepository(baseDir);
        StateStore  = new StateStore(baseDir);
        DataDir     = baseDir;
        StorageMode = StorageMode.Json;
        Reload();
    }

    public void Reload()
    {
        Sections = MedsRepo.Load();
        State    = StateStore.Load();

        // ── Синхронизация Lang ↔ LanguagePy ────────────────────────
        if (!string.IsNullOrEmpty(State.Settings.LanguagePy))
            State.Settings.Lang = State.Settings.LanguagePy!;

        if (!string.IsNullOrEmpty(State.Settings.Language))
            Loc.SetLang(State.Settings.Language);

        // ── Данные пациента из _patient (Python-формат) ─────────────
        // Python хранит карточку пациента в state["_patient"], а не в
        // _settings. Если _settings.patient_name не задано — читаем оттуда.
        if (State.RawExtras.TryGetValue("_patient", out var ptToken)
            && ptToken is Newtonsoft.Json.Linq.JObject ptObj)
        {
            if (string.IsNullOrEmpty(State.Settings.PatientName))
                State.Settings.PatientName = ptObj["full_name"]?.ToString()
                                          ?? ptObj["short_name"]?.ToString();
            if (string.IsNullOrEmpty(State.Settings.PatientDob))
                State.Settings.PatientDob = ptObj["birth_date"]?.ToString();
            if (string.IsNullOrEmpty(State.Settings.PatientGender))
                State.Settings.PatientGender = ptObj["gender"]?.ToString();
        }
    }

    public void SaveState() => StateStore.Save(State);
    public void SaveMedications() => MedsRepo.Save(Sections);

    public void NotifyChanged() => DataChanged?.Invoke();
}
