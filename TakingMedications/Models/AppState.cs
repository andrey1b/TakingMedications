using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TakingMedications.Models;

/// <summary>
/// Совместимое со схемой Python-версии (med_state_v50.json) хранилище
/// отметок «принято». Каждая дата (ключ ISO «YYYY-MM-DD») содержит словарь
/// "{medId}_taken": bool. Служебные ключи (`_finance`, `_courses`, ...)
/// сохраняются "as is" в RawExtras и переписываются обратно в файл, чтобы
/// не повредить данные, созданные питон-версией.
/// </summary>
public class AppState
{
    private const string TakenSuffix = "_taken";
    private const string SettingsKey = "_settings";
    private const string PressureKey = "_pressure_log";
    private const string PurchasesKey = "_purchases";
    private const string NotesKey = "_notes";

    /// <summary>Сохранённые отметки: дата → medId → bool.</summary>
    public Dictionary<string, Dictionary<string, bool>> Marks { get; }
        = new();

    /// <summary>Журнал давления (ts/sys/dia/pulse).</summary>
    public List<PressureEntry> PressureLog { get; set; } = new();

    /// <summary>Журнал покупок (date/medId/amount).</summary>
    public List<PurchaseEntry> Purchases { get; set; } = new();

    /// <summary>Заметки пользователя.</summary>
    public List<NoteEntry> Notes { get; set; } = new();

    /// <summary>Настройки приложения (язык, имя пациента, ...).</summary>
    public AppSettings Settings { get; set; } = new();

    /// <summary>Все ключи верхнего уровня, которые мы сами не интерпретируем
    /// (`_finance`, `_courses`, `_ui`, ...). Сохраняются как есть.</summary>
    public Dictionary<string, JToken> RawExtras { get; } = new();

    public bool IsTaken(string isoDate, string medId)
        => Marks.TryGetValue(isoDate, out var d)
           && d.TryGetValue(medId, out var v) && v;

    public void SetTaken(string isoDate, string medId, bool taken)
    {
        if (!Marks.TryGetValue(isoDate, out var day))
        {
            day = new Dictionary<string, bool>();
            Marks[isoDate] = day;
        }
        day[medId] = taken;
    }

    /// <summary>Количество отметок «принято» на эту дату.</summary>
    public int CountTaken(string isoDate)
    {
        return Marks.TryGetValue(isoDate, out var d)
            ? d.Values.Count(v => v)
            : 0;
    }

    /// <summary>Есть ли вообще какие-либо записи (true/false) на эту дату.</summary>
    public bool HasAnyMarks(string isoDate)
    {
        return Marks.TryGetValue(isoDate, out var d) && d.Count > 0;
    }

    public static AppState FromJson(string json)
    {
        var state = new AppState();
        if (string.IsNullOrWhiteSpace(json)) return state;

        var root = JObject.Parse(json);
        foreach (var prop in root.Properties())
        {
            if (IsDateKey(prop.Name) && prop.Value is JObject dayObj)
            {
                var day = new Dictionary<string, bool>();
                foreach (var p in dayObj.Properties())
                {
                    if (p.Name.EndsWith(TakenSuffix) && p.Value.Type == JTokenType.Boolean)
                    {
                        var medId = p.Name[..^TakenSuffix.Length];
                        day[medId] = p.Value.ToObject<bool>();
                    }
                }
                if (day.Count > 0) state.Marks[prop.Name] = day;
            }
            else if (prop.Name == SettingsKey && prop.Value is JObject settingsObj)
            {
                state.Settings = settingsObj.ToObject<AppSettings>() ?? new AppSettings();
            }
            else if (prop.Name == PressureKey && prop.Value is JArray arr)
            {
                state.PressureLog = arr.ToObject<List<PressureEntry>>() ?? new();
            }
            else if (prop.Name == PurchasesKey && prop.Value is JArray arr2)
            {
                state.Purchases = arr2.ToObject<List<PurchaseEntry>>() ?? new();
            }
            else if (prop.Name == NotesKey && prop.Value is JArray arr3)
            {
                state.Notes = arr3.ToObject<List<NoteEntry>>() ?? new();
            }
            else
            {
                state.RawExtras[prop.Name] = prop.Value.DeepClone();
            }
        }
        return state;
    }

    public string ToJson()
    {
        var root = new JObject();
        foreach (var kv in RawExtras) root[kv.Key] = kv.Value;
        root[SettingsKey]  = JObject.FromObject(Settings);
        root[PressureKey]  = JArray.FromObject(PressureLog);
        root[PurchasesKey] = JArray.FromObject(Purchases);
        root[NotesKey]     = JArray.FromObject(Notes);

        foreach (var (date, day) in Marks)
        {
            var obj = new JObject();
            foreach (var (medId, taken) in day) obj[medId + TakenSuffix] = taken;
            root[date] = obj;
        }
        return root.ToString(Formatting.Indented);
    }

    private static bool IsDateKey(string s)
        => s.Length == 10 && s[4] == '-' && s[7] == '-'
           && int.TryParse(s.AsSpan(0, 4), out _)
           && int.TryParse(s.AsSpan(5, 2), out _)
           && int.TryParse(s.AsSpan(8, 2), out _);
}
