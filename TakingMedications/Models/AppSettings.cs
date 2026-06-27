using Newtonsoft.Json;

namespace TakingMedications.Models;

/// <summary>
/// Настройки уровня приложения. Сохраняются в <c>state["_settings"]</c>.
///
/// ── СИНХРОНИЗАЦИЯ С PYTHON v59 ─────────────────────────────────────
/// Этот класс должен совпадать по JSON-схеме с <c>state["_settings"]</c>
/// в Python-проекте (Medication/). См. MAPPING_PY_CS.md, раздел 3.
///
/// ⚠️ ИЗВЕСТНЫЕ РАСХОЖДЕНИЯ (требуют решения автором C#):
///   • <c>"lang"</c> в C# vs <c>"language"</c> в Python.
///     Сейчас оба варианта читаются (см. <see cref="Language"/>),
///     при записи C# пишет оба ключа. Полная миграция — выбрать один,
///     исправить и удалить дубликат.
///   • <c>"patient_name"</c> в settings vs <c>state["_patient"]["full_name"]</c>
///     в Python (отдельный объект). C# хранит лишь имя, у Python —
///     полная карточка (birth_date, gender, period, ...). Для simple
///     случая совместимо, для multi-profile в Python есть Profile.
/// </summary>
public class AppSettings
{
    // ── Существующие поля (не трогаем — могут зависеть от внутренней
    // логики автора C#-порта). См. расхождения выше.

    /// <summary>Legacy-имя ключа от C# v1. Сохраняется для backward-compat.</summary>
    [JsonProperty("lang")]
    public string Lang { get; set; } = "ru";

    [JsonProperty("patient_name", NullValueHandling = NullValueHandling.Ignore)]
    public string? PatientName { get; set; }

    [JsonProperty("patient_dob", NullValueHandling = NullValueHandling.Ignore)]
    public string? PatientDob { get; set; }

    [JsonProperty("patient_gender", NullValueHandling = NullValueHandling.Ignore)]
    public string? PatientGender { get; set; }

    [JsonProperty("reminders_enabled", NullValueHandling = NullValueHandling.Ignore)]
    public bool RemindersEnabled { get; set; } = false;

    // ── Поля, добавленные для совместимости с Python v59
    // (см. MAPPING_PY_CS.md, раздел 3). Все nullable / опциональны
    // с разумными default'ами — старые state.json без этих ключей
    // продолжают грузиться без проблем.

    /// <summary>Канонический ключ языка из Python. C# пишет одновременно
    /// в <see cref="Lang"/> и сюда; читать рекомендуется через свойство
    /// <see cref="Language"/>, которое возвращает первое непустое.</summary>
    [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
    public string? LanguagePy { get; set; }

    /// <summary>v55+: голосовые TTS-напоминания (независимый канал от toast).
    /// false = только toast, true = toast + голос (или только голос если
    /// <see cref="RemindersEnabled"/>=false).</summary>
    [JsonProperty("voice_reminders_enabled", NullValueHandling = NullValueHandling.Ignore)]
    public bool VoiceRemindersEnabled { get; set; } = false;

    /// <summary>v56+: имя SAPI5-голоса для TTS. <c>null</c> = «авто по
    /// языку с приоритетом RHVoice (Elena, Anna, Mikhail, ...)».
    /// Иначе — точное имя голоса (например, "Microsoft Irina Desktop").</summary>
    [JsonProperty("voice_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? VoiceId { get; set; }

    /// <summary>v15+: за сколько минут до приёма показывать напоминание.</summary>
    [JsonProperty("reminder_advance_min", NullValueHandling = NullValueHandling.Ignore)]
    public int ReminderAdvanceMin { get; set; } = 5;

    /// <summary>Режим фонового запуска: "none" | "tray" | "start_with_windows".</summary>
    [JsonProperty("background_mode", NullValueHandling = NullValueHandling.Ignore)]
    public string BackgroundMode { get; set; } = "none";

    [JsonProperty("telegram_enabled", NullValueHandling = NullValueHandling.Ignore)]
    public bool TelegramEnabled { get; set; } = false;

    [JsonProperty("telegram_bot_token", NullValueHandling = NullValueHandling.Ignore)]
    public string TelegramBotToken { get; set; } = "";

    [JsonProperty("telegram_chat_id", NullValueHandling = NullValueHandling.Ignore)]
    public string TelegramChatId { get; set; } = "";

    /// <summary>v22+: имя preset'а цвета рамки секций или конкретный hex.</summary>
    [JsonProperty("border_color_preset", NullValueHandling = NullValueHandling.Ignore)]
    public string BorderColorPreset { get; set; } = "default";

    /// <summary>"dark" (по умолчанию) или "light" (тема Windows).</summary>
    [JsonProperty("theme", NullValueHandling = NullValueHandling.Ignore)]
    public string Theme { get; set; } = "dark";

    /// <summary>v57+: путь к папке с .mp3 для модуля «Аудиокниги».
    /// <c>null</c> = default <c>~/Documents/Audiobooks/</c>.</summary>
    [JsonProperty("audiobooks_dir", NullValueHandling = NullValueHandling.Ignore)]
    public string? AudiobooksDir { get; set; }

    /// <summary>One-shot ключ для восстановления геометрии окна после
    /// перезапуска (например, после смены языка). После применения
    /// удаляется. Не сериализуется если null.</summary>
    [JsonProperty("_pending_restore_geometry", NullValueHandling = NullValueHandling.Ignore)]
    public string? PendingRestoreGeometry { get; set; }

    // ── Удобные свойства (не сериализуются) ─────────────────────────

    /// <summary>Возвращает «эффективный» язык: сначала из канонического
    /// <c>"language"</c> (Python-стиль), затем legacy <c>"lang"</c>.
    /// Используйте это свойство в коде вместо прямого обращения.</summary>
    [JsonIgnore]
    public string Language
    {
        get => !string.IsNullOrEmpty(LanguagePy) ? LanguagePy! : Lang;
        set
        {
            LanguagePy = value;
            Lang = value;  // дублируем для backward-compat
        }
    }
}
