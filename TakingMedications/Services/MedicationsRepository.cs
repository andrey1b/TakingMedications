using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TakingMedications.Common;
using TakingMedications.Models;

namespace TakingMedications.Services;

/// <summary>
/// Загрузка и сохранение списка препаратов (medications.json).
/// На первом запуске создаёт файл из medications_default.json.
/// Совместим по структуре с Python-версией.
///
/// ── СОВМЕСТИМОСТЬ С MULTI-PROFILE (v1.2+) ────────────────────────
/// Когда baseDir = папка активного профиля (<see cref="AppPaths.ResolveDataDir"/>),
/// файл <c>medications_default.json</c> в ней может отсутствовать —
/// он лежит рядом с exe (как datas-ресурс PyInstaller / build output).
/// Поэтому при поиске default'а проверяются ОБЕ локации: data-папка
/// и exe-папка.
/// </summary>
public class MedicationsRepository : IMedicationsRepository
{
    private readonly string _path;
    private readonly string _defaultPath;
    private readonly string _exeDefaultPath;  // fallback к default из exe-папки

    public MedicationsRepository(string baseDir)
    {
        _path           = Path.Combine(baseDir, "medications.json");
        _defaultPath    = Path.Combine(baseDir, "medications_default.json");
        _exeDefaultPath = Path.Combine(PathHelpers.BaseDir, "medications_default.json");
    }

    public List<MedicationSection> Load()
    {
        if (!File.Exists(_path))
        {
            // 1. Default рядом с этим medications.json (data-папка профиля).
            if (File.Exists(_defaultPath))
                File.Copy(_defaultPath, _path);
            // 2. Fallback: default рядом с exe (стандартное место поставки).
            else if (File.Exists(_exeDefaultPath))
                File.Copy(_exeDefaultPath, _path);
            // 3. Совсем нечего — создаём пустой seed по умолчанию.
            else
                SaveSeed();
        }

        var json = File.ReadAllText(_path);
        var sections = JsonConvert.DeserializeObject<List<MedicationSection>>(json)
                       ?? new List<MedicationSection>();

        var defaults = MedicationSection.CreateDefaults();
        var byKey = sections.ToDictionary(s => s.SectionKey, s => s);
        var ordered = new List<MedicationSection>();
        foreach (var d in defaults)
        {
            if (byKey.TryGetValue(d.SectionKey, out var s)) ordered.Add(s);
            else ordered.Add(new MedicationSection { SectionKey = d.SectionKey, Title = d.Title });
        }

        foreach (var s in ordered)
            foreach (var m in s.Items) m.SectionKey = s.SectionKey;

        return ordered;
    }

    public void Save(IEnumerable<MedicationSection> sections)
    {
        var json = JsonConvert.SerializeObject(sections, Formatting.Indented);
        AtomicWrite(_path, json);
    }

    private void SaveSeed() => Save(MedicationSection.CreateDefaults());

    internal static void AtomicWrite(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }
}
