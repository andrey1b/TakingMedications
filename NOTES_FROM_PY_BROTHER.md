# Заметки от Python-брата

**Привет!** 👋 Я — Claude, который работает с Python-версией
«Приём лекарств». 6 мая 2026 пациент попросил меня помочь
синхронизировать твой C#-порт с Python production v59. Внёс
несколько минимально-инвазивных правок — всё описано ниже.

Если что-то не нравится / противоречит твоим планам — **смело
откатывай**. Я старался не лезть в архитектурные решения.

---

## TL;DR — что изменено

| Файл | Что | Зачем |
|---|---|---|
| `Models/AppSettings.cs` | +9 полей, свойство `Language` | Совместимость с Python `_settings` |
| `Common/AppPaths.cs` (NEW) | Резолв путей по логике Python | Multi-profile, общий state-файл |
| `Services/StateStore.cs` | `med_state_v1.json` → `med_state_v41.json` + one-shot migration | Тот же файл что у Python |
| `Common/MedAppContext.cs` | Default-конструктор использует `AppPaths` | Автоматический pickup активного профиля |
| `Models/PressureEntry.cs` | + поле `Sugar` (double?) | **Bug-fix**: round-trip терял `sugar` Python-записей |
| `Services/MedicationsRepository.cs` | Fallback default из exe-папки | Работа после смены baseDir на профильную папку |

`dotnet build` прошёл с **0 errors, 0 warnings** после каждого
изменения. Все детали в [MAPPING_PY_CS.md](MAPPING_PY_CS.md).

---

## ⚠️ Главное, что сломалось бы у пациента (исправлено)

### 1. Разные state-файлы

**До правок:**
- C# писал `med_state_v1.json` рядом с exe.
- Python пишет `med_state_v41.json` в `%APPDATA%/Приём лекарств/profiles/<active>/`.

Бабушка при переключении между Python и C# **видела бы пустую
программу** — два приложения работали с разными файлами.

**После:** `Common/AppPaths.cs` повторяет ту же логику резолва, что
в Python `med_profiles.py`:
1. Читает `default_profile.txt` → имя активного профиля.
2. Возвращает `<profile_dir>/med_state_v41.json` (если профиль есть).
3. Иначе fallback на exe-папку.

`StateStore` теперь использует это имя файла. One-shot миграция
`v1.json → v41.json` копирует существующий dev-state, чтобы ты ничего
не потерял.

### 2. PressureEntry терял sugar

С v52 Python пишет `sugar` (mmol/L) в каждую запись `_pressure_log`.
Newtonsoft при deserialize молча отбрасывает unknown fields, при
serialize пишет только поля модели → **C# терял sugar при первом
round-trip**.

Теперь поле в модели:
```csharp
[JsonProperty("sugar", NullValueHandling = NullValueHandling.Ignore)]
public double? Sugar { get; set; }
```

---

## 🤝 Что я НЕ трогал (твоя территория)

1. **`Services/Loc.cs`** — после анализа выяснил: ключи в C# и Python
   **намеренно разные** (C#: `history_legend_*`, `pdf_*`; Python:
   `hist_*`, `bp_cat_*`). Нет смысла синхронизировать. Если решишь
   расширить до 7 языков (DE/ES/FR/IT) — это твой выбор и темп.

2. **Все `Views/*.xaml(.cs)`** — твой WPF, твоя архитектура. Не лез.

3. **`PurchaseEntry` / `_purchases`** — ты сам написал в комментарии
   что выбрал плоский журнал вместо Python `_finance` matrix. Это
   архитектурное решение. Я только зафиксировал в MAPPING как known
   divergence. Решение объединить или оставить раздельно — за тобой.

---

## 📋 Что хочется обсудить (когда будет время)

### Вопросы, на которые лучше тебе ответить

1. **`lang` vs `language`** — C# исторически использует `lang`,
   Python — `language`. Я добавил свойство `AppSettings.Language`,
   которое читает оба и пишет в оба. Это компромисс на сейчас.
   **Долгосрочно**: выбрать один и удалить дубликат. Я бы
   предпочёл `language` (как Python production), но решать тебе.

2. **`patient_name` в settings** — у Python нет такого поля, имя
   живёт в `state["_patient"].full_name` (объект с birth_date,
   gender, period, language). Если планируешь полноценный
   multi-profile — нужна модель `Patient`. Если останешься на
   single-patient — текущий `PatientName` в settings ок.

3. **`_purchases` vs `_finance`** — описано выше. Если хочешь
   объединить — могу помочь с `FinanceMatrix` моделью. Если нет —
   просто не пересылай эти данные между приложениями (сейчас
   `RawExtras` сохраняет другую сторону forward-compat, не теряя
   данные).

### Гэпы до feature parity с Python v59

См. [MAPPING_PY_CS.md, раздел 5 «Gaps»](MAPPING_PY_CS.md). Кратко:

🔴 **Critical** (без них программа неполноценна для бабушки):
- Multi-profile (профили, wizard первого запуска, switching).
- Reminders loop + persist shown.

🟡 **Important** (фича заметная, ценится):
- Графики (ежедневное, тренд, расходы).
- Forecast блистеров.
- Документы пациента.

🟢 **Nice-to-have** (украшения):
- TTS голос напоминаний.
- Аудиокниги.
- Telegram.

Python production уже на v59, идёт активно — но это не давит на
тебя. Развивай C# в спокойном темпе.

---

## 🔄 Как поддерживать синхронизацию дальше

См. [MAPPING_PY_CS.md, раздел 7 «Правила синхронизации»](MAPPING_PY_CS.md).

Кратко:
- Файл `MAPPING_PY_CS.md` живёт **в обеих папках** идентичными
  копиями. Любые изменения — в обе сразу.
- Когда что-то меняется в Python (новая фича / новый ключ
  state.json) — Python-сторона записывает в MAPPING.
- Когда реализуешь gap — пометь его статус в разделе 5 или удали
  пункт.
- Конфликты разрешаются в пользу Python (production у бабушки).

---

## 📞 Если что-то непонятно

Все мои комментарии в коде помечены **"v1.1"**, **"v1.2"**, **"v1.2.1"**
с датами — поищи их через `grep -r "v1.2" Models/ Services/ Common/`.

Подробное описание каждой правки + причины — в MAPPING_PY_CS.md
раздел «Версионная динамика».

Удачи! 🚀

— Claude (Python-сторона), 6 мая 2026
