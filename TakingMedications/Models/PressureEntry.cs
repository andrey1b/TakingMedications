using System;
using Newtonsoft.Json;

namespace TakingMedications.Models;

/// <summary>
/// Запись об измерении АД.
///
/// Совместимо с Python-форматом
/// <c>state["_pressure_log"][i] = {"ts": "YYYY-MM-DD HH:MM",
/// "sys": int, "dia": int, "pulse": int?, "sugar": float?}</c>.
///
/// ── ИСТОРИЯ ─────────────────────────────────────────────────────────
/// • v51 Python: ts/sys/dia/pulse.
/// • v52 Python: добавлено поле <c>sugar</c> (mmol/L, опционально) —
///   диалог АД получил поле «Сахар» с валидацией 1.0–30.0.
/// • C# v1.0: имел только ts/sys/dia/pulse.
/// • C# v1.2 (6 мая 2026): добавлено <c>Sugar</c> — без него C# терял
///   sugar при first read+save цикле для записей, созданных Python.
/// </summary>
public class PressureEntry
{
    [JsonProperty("ts")]    public string Timestamp { get; set; } = "";
    [JsonProperty("sys")]   public int    Systolic  { get; set; }
    [JsonProperty("dia")]   public int    Diastolic { get; set; }

    [JsonProperty("pulse", NullValueHandling = NullValueHandling.Ignore)]
    public int? Pulse { get; set; }

    /// <summary>
    /// Уровень сахара в крови, mmol/L (опционально, v52+).
    /// Норма натощак 3.9–5.5; гипогликемия &lt; 3.0; тяжёлая
    /// гипергликемия &gt; 15. Валидационный диапазон в Python:
    /// 1.0–30.0; «медицински необычный» (с confirm-диалогом):
    /// вне 3.0–20.0.
    /// </summary>
    [JsonProperty("sugar", NullValueHandling = NullValueHandling.Ignore)]
    public double? Sugar { get; set; }

    public DateTime ParsedTimestamp
    {
        get
        {
            if (DateTime.TryParseExact(Timestamp, "yyyy-MM-dd HH:mm",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
                return dt;
            return DateTime.MinValue;
        }
    }
}
