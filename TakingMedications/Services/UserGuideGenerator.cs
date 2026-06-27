using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TakingMedications.Services;

public static class UserGuideGenerator
{
    // Цвета палитры
    private const string Accent   = "#6C63FF";
    private const string Dark     = "#1A1A2E";
    private const string Card     = "#16213E";
    private const string TextMain = "#EAEAEA";
    private const string TextMute = "#A0A0B8";
    private const string White    = "#FFFFFF";
    private const string Gray1    = "#F7F7FD";
    private const string Gray2    = "#E8E8F0";
    private const string TextDark = "#1A1A2E";
    private const string TextSub  = "#444455";
    private const string BorderC  = "#DDDDF0";

    public static void Generate(string outputPath, string shotsDir)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(11));
                page.Content().Column(col =>
                {
                    col.Item().Element(TitlePage);
                    col.Item().PageBreak();
                    col.Item().Element(c => SchedulePage(c, shotsDir));
                    col.Item().PageBreak();
                    col.Item().Element(c => PressurePage(c, shotsDir));
                    col.Item().PageBreak();
                    col.Item().Element(c => SettingsPage(c, shotsDir));
                    col.Item().PageBreak();
                    col.Item().Element(QuickRefPage);
                });
            });
        }).GeneratePdf(outputPath);
    }

    // ── Обложка ──────────────────────────────────────────────────────

    private static void TitlePage(IContainer c)
    {
        c.Background(Dark).Column(col =>
        {
            col.Item().Height(100);
            col.Item().PaddingHorizontal(60).Column(inner =>
            {
                inner.Item().Text("Приём лекарств")
                     .FontSize(44).Bold().FontColor(Accent);
                inner.Item().Height(10);
                inner.Item().Text("Руководство пользователя")
                     .FontSize(22).FontColor(TextMute);
                inner.Item().Height(6);
                inner.Item().Text("Программа для учёта приёма лекарств")
                     .FontSize(14).FontColor(TextMain);
            });
            col.Item().Height(30);
            col.Item().PaddingHorizontal(60).Height(3).Background(Accent);
            col.Item().Height(30);

            // Три строки по 3 чипа
            col.Item().PaddingHorizontal(60).Row(row =>
            {
                Chip(row.RelativeItem(), "📅", "Расписание на каждый день");
                row.ConstantItem(12);
                Chip(row.RelativeItem(), "🩺", "Давление и сахар");
                row.ConstantItem(12);
                Chip(row.RelativeItem(), "📄", "PDF-отчёт для врача");
            });
            col.Item().Height(12);
            col.Item().PaddingHorizontal(60).Row(row =>
            {
                Chip(row.RelativeItem(), "🔔", "Напоминания о приёме");
                row.ConstantItem(12);
                Chip(row.RelativeItem(), "📊", "История приёма");
                row.ConstantItem(12);
                Chip(row.RelativeItem(), "🎨", "Светлая и тёмная тема");
            });
            col.Item().Height(50);
            col.Item().PaddingHorizontal(60).Height(1).Background(Card);
            col.Item().Height(16);
            col.Item().PaddingHorizontal(60)
               .Text($"Windows 10/11  ·  Не требует установки .NET  ·  {DateTime.Now.Year}")
               .FontSize(11).FontColor(TextMute);
        });
    }

    private static void Chip(IContainer c, string icon, string text)
    {
        c.Background(Card).Padding(14).Column(col =>
        {
            col.Item().Text(icon).FontSize(22).AlignCenter();
            col.Item().Height(6);
            col.Item().Text(text).FontSize(10).FontColor(TextMain)
               .AlignCenter().LineHeight(1.4f);
        });
    }

    // ── Расписание ────────────────────────────────────────────────────

    private static void SchedulePage(IContainer c, string shotsDir)
    {
        c.Padding(40).Column(col =>
        {
            H1(col, "Главный экран — расписание");
            col.Item().Height(12);

            var lightShot = Shot(shotsDir, "_guide_schedule_light.png", "_guide_main.png");
            if (lightShot != null)
                col.Item().Image(lightShot).FitWidth();

            col.Item().Height(14);
            InfoTable(col, new[]
            {
                ("📅 Расписание",
                 "Показывает все лекарства на выбранный день. Поставьте галочку □ — лекарство принято. Внизу счётчик «Принято X из N»."),
                ("◀ ▶  Листать дни",
                 "Кнопками влево/вправо или нажмите «Сегодня». «Сейчас» — прокрутка к текущему времени суток."),
                ("▼ УТРО / ДЕНЬ…",
                 "Секции раскрываются и сворачиваются щелчком по заголовку. Активная — подсвечена цветом."),
                ("📋 Список лекарств",
                 "Кнопка внизу справа — открывает редактор: добавить, изменить, удалить препарат, указать врача и курс."),
            });

            col.Item().Height(18);
            H1(col, "Тёмная и светлая тема");
            col.Item().Height(10);

            var darkShot = Shot(shotsDir, "_guide_dark.png");
            if (lightShot != null && darkShot != null)
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Image(lightShot).FitWidth();
                    row.ConstantItem(8);
                    row.RelativeItem().Image(darkShot).FitWidth();
                });
                col.Item().Height(4);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("☀  Светлая (Windows)")
                       .FontSize(9).FontColor(TextSub).AlignCenter();
                    row.ConstantItem(8);
                    row.RelativeItem().Text("🌙 Тёмная")
                       .FontSize(9).FontColor(TextSub).AlignCenter();
                });
            }
        });
    }

    // ── Давление ─────────────────────────────────────────────────────

    private static void PressurePage(IContainer c, string shotsDir)
    {
        c.Padding(40).Column(col =>
        {
            H1(col, "Артериальное давление и сахар");
            col.Item().Height(12);

            var shot = Shot(shotsDir, "_guide_pressure.png");
            if (shot != null)
                col.Item().Row(r => { r.RelativeItem(5).Image(shot).FitWidth(); r.RelativeItem(1); });

            col.Item().Height(14);
            InfoTable(col, new[]
            {
                ("Ввод данных",
                 "Заполните: Пульс, Систолическое (верхнее), Диастолическое (нижнее), Сахар (ммоль/л — необязательно). Нажмите «Сохранить»."),
                ("Цвет цифр",
                 "Зелёный — Оптимальное;  Жёлтый — Высокое нормальное;  Оранжевый — АГ I ст.;  Красный — АГ II ст."),
                ("Вкладка График",
                 "Показывает динамику давления за все измерения. Удобно показать врачу на приёме."),
                ("Удаление строки",
                 "Щёлкните по строке — она выделится. Нажмите «✕ Удалить выбранную» внизу."),
            });

            col.Item().Height(20);
            H1(col, "История приёма");
            col.Item().Height(10);

            var histShot = Shot(shotsDir, "_guide_history.png");
            if (histShot != null)
                col.Item().Image(histShot).FitWidth();

            col.Item().Height(10);
            col.Item().Background(Gray1).Padding(10)
               .Text("Вкладка «История» показывает цветной календарь: 🟢 все приняты, 🟡 частично, 🔴 пропущено. " +
                     "Щёлкните по любому дню — справа появится подробный список.")
               .FontSize(10).FontColor(TextSub).LineHeight(1.5f);
        });
    }

    // ── Настройки ─────────────────────────────────────────────────────

    private static void SettingsPage(IContainer c, string shotsDir)
    {
        c.Padding(40).Column(col =>
        {
            H1(col, "Настройки");
            col.Item().Height(12);

            var shot = Shot(shotsDir, "_guide_settings.png");
            if (shot != null)
                col.Item().Row(r => { r.RelativeItem(4).Image(shot).FitWidth(); r.RelativeItem(2); });

            col.Item().Height(14);
            InfoTable(col, new[]
            {
                ("Язык",
                 "Русский, Українська, English, Español, Deutsch, Français, Italiano. Смена мгновенная — без перезапуска."),
                ("Тема",
                 "☀ Светлая (Windows) — белый фон, высокий контраст.\n🌙 Тёмная — комфортна вечером."),
                ("Пациент",
                 "ФИО отображается в шапке и в PDF-отчёте для врача."),
                ("Режим запуска",
                 "«В трее» — остаётся в фоне при закрытии окна. «Автозапуск» — стартует вместе с Windows."),
            });

            col.Item().Height(20);
            H1(col, "Как добавить лекарство");
            col.Item().Height(10);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c2 => { c2.ConstantColumn(28); c2.RelativeColumn(); });
                Step(table, "1", "Нажмите «📋 Список лекарств» внизу справа главного окна.");
                Step(table, "2", "Нажмите «＋ Добавить».");
                Step(table, "3", "Заполните: Название, период (Утро/День/Вечер/Ночь), время, примечание, врач, курс.");
                Step(table, "4", "Нажмите «💾 Сохранить» — лекарство появится в расписании.");
            });
        });
    }

    // ── Справочник ────────────────────────────────────────────────────

    private static void QuickRefPage(IContainer c)
    {
        c.Padding(40).Column(col =>
        {
            H1(col, "Краткий справочник кнопок");
            col.Item().Height(12);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c2 =>
                {
                    c2.RelativeColumn(2);
                    c2.RelativeColumn(4);
                });
                table.Header(h =>
                {
                    h.Cell().Background(Accent).Padding(8)
                     .Text("Кнопка / действие").Bold().FontColor(White).FontSize(10);
                    h.Cell().Background(Accent).Padding(8)
                     .Text("Что делает").Bold().FontColor(White).FontSize(10);
                });

                string[,] rows =
                {
                    { "📄 Отчёт PDF",       "Создать PDF-отчёт за выбранный период для врача" },
                    { "🩺 Давление",        "Записать давление / пульс / сахар" },
                    { "🔔 Напоминания",     "Включить / выключить всплывающие напоминания" },
                    { "⚙ Настройки",        "Язык, тема, имя пациента, режим запуска" },
                    { "◀ ▶",               "Листать дни назад / вперёд" },
                    { "Сегодня",            "Перейти к сегодняшнему расписанию" },
                    { "Сейчас",             "Прокрутить к секции текущего времени суток" },
                    { "▼ заголовок секции", "Свернуть / развернуть блок (УТРО, ДЕНЬ…)" },
                    { "□ у лекарства",      "Отметить принятым — поставить галочку ✓" },
                    { "📋 Список лекарств", "Открыть редактор препаратов" },
                };

                for (int i = 0; i < rows.GetLength(0); i++)
                {
                    var bg = i % 2 == 0 ? Gray1 : White;
                    table.Cell().Background(bg).Padding(7)
                         .Text(rows[i, 0]).FontSize(10).Bold().FontColor(Accent);
                    table.Cell().Background(bg).Padding(7)
                         .Text(rows[i, 1]).FontSize(10).FontColor(TextSub);
                }
            });

            col.Item().Height(20);
            H1(col, "Где хранятся данные");
            col.Item().Height(8);
            col.Item().Background(Gray1).Padding(12)
               .Text(@"C:\Users\<имя пользователя>\AppData\Roaming\Приём лекарств\")
               .FontFamily("Courier New").FontSize(10).FontColor("#333355");
            col.Item().Height(6);
            col.Item().Text(
                "Данные хранятся в системной папке Windows — они НЕ потеряются при перемещении " +
                "или обновлении программы. Для резервной копии достаточно скопировать эту папку целиком.")
               .FontSize(10).FontColor(TextSub).LineHeight(1.5f);

            col.Item().Height(20);
            col.Item().Height(2).Background(Accent);
            col.Item().Height(8);
            col.Item().Row(row =>
            {
                row.RelativeItem()
                   .Text("Windows 10 / 11 (64-бит)  ·  Установка не требуется  ·  ~191 МБ")
                   .FontSize(9).FontColor(TextMute);
                row.AutoItem()
                   .Text($"© {DateTime.Now.Year}")
                   .FontSize(9).FontColor(TextMute);
            });
        });
    }

    // ── Вспомогательные ───────────────────────────────────────────────

    private static void H1(ColumnDescriptor col, string title)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(5).Background(Accent);
            row.ConstantItem(10);
            row.RelativeItem()
               .Text(title).FontSize(16).Bold().FontColor(TextDark);
        });
    }

    private static void InfoTable(ColumnDescriptor col, (string label, string text)[] rows)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(3); });
            foreach (var (label, text) in rows)
            {
                table.Cell().Padding(7).PaddingRight(4)
                     .Text(label).FontSize(10).Bold().FontColor(Accent);
                table.Cell().Padding(7).PaddingLeft(4)
                     .Text(text).FontSize(10).FontColor(TextSub).LineHeight(1.4f);
            }
        });
    }

    private static void Step(TableDescriptor table, string num, string text)
    {
        table.Cell().Padding(6).Background(Accent)
             .AlignCenter().AlignMiddle()
             .Text(num).FontSize(11).Bold().FontColor(White);
        table.Cell().Padding(6).PaddingLeft(10)
             .Text(text).FontSize(10).FontColor(TextSub).LineHeight(1.4f);
    }

    private static string? Shot(string dir, params string[] names)
    {
        foreach (var name in names)
        {
            var p = Path.Combine(dir, name);
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
