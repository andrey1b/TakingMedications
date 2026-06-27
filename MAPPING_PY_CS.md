# Python ↔ C# Mapping

**Single source of truth** для синхронизации двух реализаций
«Приём лекарств»: Python (`Medication/`, v59) и C# WPF
(`Taking medications/TakingMedications/`).

> Этот файл идентичен в обоих репозиториях. Любые изменения —
> вносим в **обе** копии одновременно. При расхождении —
> доверяй версии в `Medication/` (Python — production).

**Дата актуальности:** 6 мая 2026 · Python v59 · C# v1 (port from v50)

---

## Принцип

Имена классов, файлов и методов **не должны совпадать 1-в-1**.
Python использует `snake_case` + `med_` префикс + плоскую структуру
из 31 модуля. C# использует `PascalCase` + namespace
`TakingMedications.{Common,Models,Services,Views}` + MVVM-разделение.
**Оба варианта идиоматичны** для своих языков. Заставлять одну
сторону копировать стиль другой — антипаттерн.

**Что ДОЛЖНО совпадать:**
1. Формат файла `med_state_v41.json` (оба читают/пишут один файл).
2. Имена ключей в JSON (snake_case) — независимо от стиля кода.
3. Имена ключей `i18n` (одинаковые в `locales/*.json` и в C# `Loc`).
4. Семантика фичей — кнопка «🔔» делает то же в обеих программах.

---

## 1. Mapping модулей

### Утилиты и инфраструктура

| Python (med_*.py) | C# | Совпадает? | Заметки |
|---|---|:---:|---|
| `med_utils` | `Common/PathHelpers` + `Common/MedAppContext` | ⚠️ | C# разделил утилиты на 2 файла. Палитра `COLORS` в C# идёт через WPF-ресурсы (XAML), не через словарь |
| `med_i18n` | `Services/Loc` | ⚠️ | C# embedded словари (3 языка), Python — JSON × 7 языков |
| `med_state_migration` | (в `StateStore`) | ❌ | Legacy-миграция (LEGACY_STATE_NAMES) в C# не реализована |
| (state-логика inline в pyw) | `Services/StateStore` + `Models/AppState` | ✅ | C# чище разделил — это **правильнее**, можем перенять идею |

### Сервисные модули (без UI)

| Python | C# | Совпадает? | Заметки |
|---|---|:---:|---|
| `med_pdf` | `Services/PdfReport` | ✅ | Аналогичная роль; в C# через QuestPDF, в Python через ReportLab |
| `med_interactions` | `Services/DrugInteractions` | ✅ | |
| `med_reminders` | (нет) | ❌ | Reminders loop, persist shown, missed scan — **нет в C#** |
| `med_background` | (нет, частично `TrayService`) | ⚠️ | Background mode + минимизация в трей — частично |
| `med_tray` | `Services/TrayService` | ✅ | |
| `med_telegram_setup` | (нет) | ❌ | |
| `med_tts` | (нет) | ❌ | Voice reminders через PowerShell; в C# можно `System.Speech.Synthesis` напрямую |
| `med_shopping` | (только `Models/PurchaseEntry`) | ⚠️ | Логика покупок — модель есть, диалог нет |

### UI-диалоги и окна

| Python | C# | Совпадает? | Заметки |
|---|---|:---:|---|
| `med_pressure_dialog` | `Views/PressureDialog` | ✅ | **Имя совпадает!** Эталон |
| `med_interactions_dialog` | `Views/InteractionsDialog` | ✅ | |
| `med_export_dialog` | `Views/ExportPdfDialog` | ✅ | |
| `med_edit_dialog` | `Views/MedicationEditWindow` | ⚠️ | Window vs Dialog — оба идиоматичны |
| `med_manage_dialog` | `Views/ManageMedicationsWindow` | ⚠️ | |
| `med_settings_dialog` | `Views/SettingsWindow` | ⚠️ | |
| `med_notes_dialog` | (нет) | ❌ | |
| `med_documents` | (нет) | ❌ | Хранилище документов пациента |
| `med_audiobooks` | (нет) | ❌ | Диалог .mp3 списка |

### UI-вкладки

| Python | C# | Совпадает? | Заметки |
|---|---|:---:|---|
| `med_schedule_tab` | `Views/ScheduleView` | ⚠️ | Tab vs View |
| `med_history_tab` | `Views/HistoryView` | ⚠️ | |
| `med_finance_tab` | `Views/FinanceView` | ⚠️ | |
| `med_charts_tab` | (нет) | ❌ | |
| `med_charts` | (нет) | ❌ | Renderer графиков — в C# можно ScottPlot или OxyPlot |

### Профили (multi-patient)

| Python | C# | Совпадает? | Заметки |
|---|---|:---:|---|
| `med_profiles` | (нет) | ❌ | Profile-объект, путь к profile.json |
| `med_profiles_dialog` | (нет) | ❌ | UI управления профилями |
| `med_profile_select` | (нет) | ❌ | Select-диалог при старте если несколько |
| `med_patient_wizard` | (нет) | ❌ | Мастер первого запуска |

### Forecast

| Python | C# | Совпадает? | Заметки |
|---|---|:---:|---|
| `med_forecast` | (нет; `Models/PurchaseEntry` есть) | ❌ | Расчёт блистеров, прогноз закупок |

---

## 2. Канонический формат `med_state_v41.json`

**Имя файла зафиксировано** на `med_state_v41.json` с v41 multi-profile —
не меняется при bump'е приложения. Лежит в:
- Single-profile (legacy): `<dist>/med_state_v41.json` или `<exe_dir>`
- Multi-profile: `%APPDATA%/Приём лекарств/profiles/<short_name>/med_state_v41.json`

### Резолв пути в обеих платформах (синхронизирован 6 мая 2026)

**Python**: `med_profiles_v59.STATE_FILE_NAME = "med_state_v41.json"`,
`Profile.state_file = os.path.join(profile_dir, STATE_FILE_NAME)`,
активный профиль из `%APPDATA%/Приём лекарств/default_profile.txt`.

**C#**: `Common/AppPaths.ResolveStateFilePath()` — реализует ту же
логику. До 6 мая 2026 C# использовал `med_state_v1.json` рядом с exe
(не совпадало с Python). Теперь:
1. Если `default_profile.txt` существует и папка профиля есть —
   берётся `<profile_dir>/med_state_v41.json`.
2. Иначе fallback на `<exe_dir>/med_state_v41.json`
   (single-profile legacy).
3. One-shot миграция legacy `med_state_v1.json` → `med_state_v41.json`
   при первом запуске после смены имени
   (`AppPaths.MigrateLegacyV1StateIfNeeded`).

**Результат:** оба приложения читают и пишут **один и тот же файл**.
У бабушки Бучина → оба видят
`C:\Users\User\AppData\Roaming\Приём лекарств\profiles\Бучин\med_state_v41.json`.

### Schema (все ключи snake_case)

```jsonc
{
  // Маркеры приёма по дням: "<YYYY-MM-DD>": {"<med_id>_taken": true, ...}
  "2026-05-06": {
    "morning_aspirin_taken": true,
    "evening_atoris_taken": false
  },

  // Журнал артериального давления (v51+) и сахара (v52+).
  // ВАЖНО: до C# v1.2.1 (6 мая 2026) C# не имел поля `sugar` в модели
  // PressureEntry — поле терялось при first save цикле. Теперь починено.
  "_pressure_log": [
    {
      "ts": "2026-05-06 09:30",  // YYYY-MM-DD HH:MM
      "sys": 130,                 // int, mmHg
      "dia": 85,                  // int, mmHg
      "pulse": 72,                // int, optional
      "sugar": 5.4                // float mmol/L, optional (v52+)
    }
  ],

  // Настройки блистеров (v52+): name_key → конфиг.
  "_blisters": {
    "aspirin": {
      "tabs": 10,                 // штук в одном блистере
      "price": 45.50              // цена блистера, optional
    }
  },

  // Финансовая таблица.
  "_finance": {
    "dates": ["01.05.2026", "08.05.2026"],
    "cells":  { "<med_id>": { "01.05.2026": "10,00", ... } },
    "counts": { "<med_id>": { "01.05.2026": 1, ... } }
  },

  // Какие напоминания уже показаны сегодня (для устойчивости к перезапуску).
  "_reminders_shown": ["09:00", "13:00"],
  "_reminders_shown_date": "2026-05-06",

  // Карточка пациента.
  "_patient": {
    "full_name": "Бучин Андрей Петрович",
    "birth_date": "1955-03-15",
    "birth_year": 1955,
    "gender": "M",
    "language": "ru",
    "period": "Май 2026"
  },

  // Настройки приложения.
  "_settings": { /* см. раздел 3 */ }
}
```

### Правила forward-compatibility

1. **Неизвестные ключи не удалять.** В C# — собрано в `RawExtras` (✅ уже сделано).
   В Python — сохраняются автоматически (json.load дает dict).
2. **Старые версии не должны падать на новых ключах.** Все новые поля
   должны быть optional с разумным default (None / false / [] / {}).
3. **Имена дат — `YYYY-MM-DD`** (для маркеров приёма) или `DD.MM.YYYY`
   (для финансовой `dates`). Не смешивать.

---

## 3. Ключи `_settings` (snake_case в JSON)

| Key (JSON) | Type | Default | Где используется | Python ✓ | C# ✓ |
|---|---|---|---|:---:|:---:|
| `language` | str | "ru" | i18n текущий | ✅ | ✅ `LanguagePy` (см. ⚠️) |
| `lang` | str | "ru" | C#-only legacy alias | ❌ | ✅ `Lang` (см. ⚠️) |
| `patient_name` | str | (none) | C#-only top-level | ❌ | ✅ `PatientName` (см. ⚠️) |
| `reminders_enabled` | bool | false | Visual toast | ✅ | ✅ `RemindersEnabled` |
| `voice_reminders_enabled` | bool | false | TTS канал (v55+) | ✅ | ✅ `VoiceRemindersEnabled` (v59+) |
| `voice_id` | str?/null | null | Имя SAPI5-голоса (v56+) | ✅ | ✅ `VoiceId` (v59+) |
| `reminder_advance_min` | int | 5 | Минут до приёма для toast | ✅ | ✅ `ReminderAdvanceMin` (v59+) |
| `background_mode` | str | "none" | "none"\|"tray"\|"start_with_windows" | ✅ | ✅ `BackgroundMode` (v59+) |
| `telegram_enabled` | bool | false | | ✅ | ✅ `TelegramEnabled` (v59+) |
| `telegram_bot_token` | str | "" | | ✅ | ✅ `TelegramBotToken` (v59+) |
| `telegram_chat_id` | str | "" | | ✅ | ✅ `TelegramChatId` (v59+) |
| `border_color_preset` | str | "default" | | ✅ | ✅ `BorderColorPreset` (v59+) |
| `audiobooks_dir` | str?/null | null | Путь к папке .mp3 (v57+) | ✅ | ✅ `AudiobooksDir` (v59+) |
| `_pending_restore_geometry` | str | (one-shot) | Геометрия после restart | ✅ | ✅ `PendingRestoreGeometry` (v59+) |

### ⚠️ Расхождения, которые нужно решить автору C#

1. **`lang` vs `language`** — Python использует `language`, C# исторически
   `lang`. Сейчас C# `AppSettings` пишет/читает **оба ключа** через
   `Language` свойство (см. `Models/AppSettings.cs:Language`). Долгосрочно
   автору C# рекомендуется выбрать один (предлагаем `language` как
   у Python production) и убрать дубликат.
2. **`patient_name`** — в Python нет такого поля в settings; имя пациента
   живёт в `state["_patient"]["full_name"]` (объект с birth_date, gender,
   period, language). C# хранит только имя — для simple-случая ок,
   для полноценного multi-profile нужна модель `Patient` или подобная.
3. **`_purchases` (C#) vs `_finance` (Python)** — две **разные модели**
   одних и тех же финансов:
   - **Python `_finance`**: матрица `{dates: [...], cells: {medId: {date: amount}}, counts: {medId: {date: int}}}`. Excel-style таблица.
   - **C# `_purchases`**: плоский журнал `[{ts, medId, amount, note}, ...]`.

   Эти данные **не пересекаются** между приложениями. Бабушка пишет
   покупку в Python — C# её не видит, и наоборот. Решение зависит от
   автора C#:
   - (a) Реализовать модель `_finance` (matrix) и удалить `_purchases`.
   - (b) Оставить `_purchases` как C#-only и не претендовать на синхронность финансов.
   - (c) Двусторонняя миграция при load/save.

   На сегодня (6 мая 2026) `_finance` в C# попадает в `RawExtras`
   (forward-compat), но не отображается. `_purchases` Python видит
   только как RawExtras.

**В C#** свойства `AppSettings` оформляются через
`[JsonProperty("reminders_enabled")]` (Newtonsoft) — чтобы JSON-имя
было snake_case даже при PascalCase свойстве.

---

## 4. Ключи i18n — НАМЕРЕННО РАЗНЫЕ

Python: `locales/{ru,uk,en,es,de,fr,it}.json` — 7 файлов, ~600 ключей.
C#: `Services/Loc.cs` — embedded словари RU/EN/UK (~250 ключей).

**Реальное состояние** (обнаружено 6 мая 2026):
- Python использует префиксы по модулям: `hist_*`, `fin_*`, `bp_cat_*`,
  `audiobooks_*`, `pressure_*`.
- C# использует более длинные/семантические имена и категории:
  `history_legend_*`, `pdf_bp_stage1`, `pdf_col_*`, `weekday_short_0`,
  `month_gen_5`.

**Расхождение примеры:**
| Python | C# |
|---|---|
| `hist_legend_full` | `history_legend_full` |
| `hist_day_pick` | `history_select_day` |
| `bp_cat_htn1` | `pdf_bp_stage1` |
| `_meta.weekdays[0]` | `weekday_short_0` |
| (нет) | `pdf_col_when`, `pdf_col_taken`, ... (PDF-only ключи) |

**Решение:** не пытаться синхронизировать. Каждая платформа имеет
свою внутренне согласованную систему ключей. Это **не баг**.

**Правила:**
1. Когда в Python добавляется новый текст — он добавляется в
   `locales/*.json` с Python-стиль ключом (`hist_*`, `fin_*`).
2. Когда в C# добавляется новый текст — он добавляется в `Loc.cs`
   с C#-стиль ключом (`history_*`, `pdf_*`).
3. **Семантика placeholder'ов общая:** `{name}`, `{meds}`, `{date}`,
   `{path}`, `{taken}`, `{total}` — одинаковы в обеих платформах.
4. **Локали для не-RU/EN/UK языков** (es, de, fr, it) живут только
   в Python. C# при необходимости расширит свои словари — это
   независимая работа.

### Группы ключей (по префиксам)

- `pressure_*` — диалог АД
- `bp_cat_*` — категории АД (5 штук: optimal, normal, high_normal, htn1, htn2)
- `hist_*` — вкладка «История»
- `fin_*` — вкладка «Финансы»
- `settings_*` — диалог настроек
- `settings_voice_*` — голосовые настройки (v56+)
- `voice_reminder_*`, `voice_missed_*` — TTS-фразы (v55+)
- `audiobooks_*` — диалог аудиокниг (v57+)
- `tip_*` — tooltip'ы тулбара
- `btn_*_short` — короткие подписи кнопок тулбара
- `reminder_toast_*`, `reminder_missed_*` — текст toast'ов
- `tg_*`, `telegram_*` — Telegram-интеграция
- `error`, `ok`, `cancel`, ... — системные

**Эталон списка:** `Medication/locales/ru.json` — самый полный.

---

## 5. Gaps: что в C# отсутствует относительно Python v59

### Critical (без них программа неполноценна для бабушки)

| Gap | Python модуль | Приоритет |
|---|---|:---:|
| Multi-profile (профили пациентов) | `med_profiles*` (3 модуля) | 🔴 |
| Wizard первого запуска | `med_patient_wizard` | 🔴 |
| Reminders loop + persist shown | `med_reminders`, `med_background` | 🔴 |
| Полная схема state-миграции | `med_state_migration` | 🟡 |

### Important (заметная фича, ценится пациентом)

| Gap | Python модуль | Приоритет |
|---|---|:---:|
| Графики (ежедневное, тренд, расходы) | `med_charts*` | 🟡 |
| Forecast блистеров | `med_forecast` | 🟡 |
| Документы пациента (хранилище + Claude/Copilot) | `med_documents` | 🟡 |
| ~~Полная локализация на 7 языков из JSON~~ | ~~`med_i18n`~~ | — (см. раздел 4: i18n намеренно разные) |
| Расширить C# `Loc.cs` до DE/ES/FR/IT (опционально) | (C# своя система) | 🟢 |

### Nice-to-have (украшения)

| Gap | Python модуль | Приоритет |
|---|---|:---:|
| 🔊 Голосовые напоминания | `med_tts` | 🟢 |
| 🎧 Аудиокниги | `med_audiobooks` | 🟢 |
| Telegram-интеграция | `med_telegram_setup` | 🟢 |
| Заметки | `med_notes_dialog` | 🟢 |

---

## 6. Версионная динамика — что нового было после Python v50

C# собран как «port from v50». С тех пор Python прошёл:

| Версия | Главное изменение | Затронутые модули |
|---|---|---|
| v51 | Independent multi-profile, раздельные `med_*_v51.py` | (вся структура) |
| v52 | АД-таблица с раздельной подсветкой sys/dia, поле «Сахар», forecast блистеров | `med_pressure_dialog`, `med_forecast` |
| v53 | Baseline bump | (нет правок контента) |
| v54 | Блок «История АД» на вкладке «История», zebra finance, sugar warn-confirm | `med_history_tab`, `med_finance_tab`, `med_pressure_dialog` |
| v55 | i18n history_tab, логи в except, **🔊 голосовые напоминания (PowerShell)** | `med_tts` (новый), `med_reminders`, `med_settings_dialog` |
| v56 | Voice picker + кнопка Test + RHVoice hint | `med_tts`, `med_settings_dialog` |
| v57 | Умный voice default (Elena→RHVoice→default), **🎧 кнопка Аудиокниги** | `med_tts`, `med_audiobooks` (новый) |
| v58 | Audiobooks empty-state UX (открыть проводник + toast); hotfix pady-tuple | `med_audiobooks` |
| v59 | Документационный релиз — переписана шапка `.pyw` как обзор | (нет правок кода) |
| C# v1.1 | (6 мая 2026) `AppSettings.cs` расширен 9 полями для совместимости | `Models/AppSettings.cs` |
| C# v1.2 | (6 мая 2026 вечер) Резолв путей синхронизирован с Python: `Common/AppPaths.cs`, имя файла `med_state_v1.json` → `med_state_v41.json`, multi-profile через `default_profile.txt`, one-shot legacy migration | `Common/AppPaths.cs` (новый), `Services/StateStore.cs`, `Common/MedAppContext.cs` |
| C# v1.2.1 | (6 мая 2026, продолжение) **Bug-fix sugar**: PressureEntry получил поле `Sugar` — раньше C# терял `sugar` Python-записей при первом read+save; **MedRepo fallback**: medications_default.json теперь ищется в data-папке профиля И в exe-папке | `Models/PressureEntry.cs`, `Services/MedicationsRepository.cs` |

**Чтобы C# догнал v59** — см. список gap'ов выше. Минимальный набор
для feature parity: `multi-profile + wizard + reminders + charts + forecast`.

---

## 7. Правила синхронизации

### Когда меняется Python

1. **Добавляется новая фича в Python** → сразу записать в этот файл,
   в раздел 5 («Gaps») с приоритетом, и в раздел 6 («Версионная
   динамика») в строку текущей версии.
2. **Добавляется ключ в `_settings`** → внести в раздел 3 с типом
   и default'ом.
3. **Добавляется новый ключ i18n** → внести в раздел 4 в нужную группу.
4. **Меняется schema state.json** → обновить раздел 2 и предупредить
   C#-коллегу что C# upgrade нужен (с учётом forward-compat).

### Когда меняется C#

1. **Реализован gap из раздела 5** → изменить статус ❌ → ✅ и убрать
   из раздела 5.
2. **Добавляется C#-only фича** (например, специфика WPF) → пометить
   её отдельным пунктом «C# extra», не пытаться портировать обратно
   в Python без причины.

### Конфликты разрешаются в пользу Python

Python — production у бабушек. Если C# делает что-то по-другому
и это меняет JSON-схему — C# обязан подстроиться, не наоборот.
**Исключение:** если Python имеет очевидный технический долг,
который C# исправляет правильно, — записать в раздел «Кандидаты
на улучшение Python» (можно создать раздел 8).

---

## 8. Кандидаты на улучшение Python (вдохновение от C#)

C# делает несколько вещей **архитектурно правильнее** Python.
Можем перенять:

1. **Разделение `StateStore` / `AppState`.** В Python state-логика
   живёт в главном `pyw`. Можно вынести в отдельный модуль
   (`med_state_v60.py`?) с явным API `load() / save() / migrate_legacy()`.
2. **Models как явные классы.** В Python `_pressure_log` — это `list[dict]`.
   Можно ввести `dataclass PressureEntry` для чистоты типов
   (Python 3.10+ dataclasses).
3. **`RawExtras` для forward-compat.** В Python мы и так сохраняем
   неизвестные ключи (json.load даёт dict), но это неявно.
   В C# это явный паттерн — стоит документировать.

Эти идеи можно подхватить в Python v60+.

---

**В конце:** этот файл — живой. Не бойся его редактировать. Лучше
многословный, но актуальный mapping, чем элегантный, но устаревший.
