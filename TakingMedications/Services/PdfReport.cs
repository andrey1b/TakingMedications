using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TakingMedications.Common;
using TakingMedications.Models;

namespace TakingMedications.Services;

/// <summary>
/// Генерация PDF-отчёта (через QuestPDF). Эквивалент Python `med_pdf.py`.
/// </summary>
public static class PdfReport
{
    // Цветовая палитра (HEX без #)
    private const string ColorPrimary = "1565C0";
    private const string ColorH1Text  = "37474F";
    private const string ColorH2Text  = "546E7A";
    private const string ColorMuted   = "78909C";
    private const string ColorSuccess = "2E7D32";
    private const string ColorWarning = "EF6C00";
    private const string ColorDanger  = "C62828";
    private const string ColorBgRow   = "F5F7FA";
    private const string ColorBorder  = "CFD8DC";

    /// <summary>
    /// Сгенерировать PDF и сохранить в outputPath.
    /// </summary>
    public static void Generate(MedAppContext ctx, ReportOptions opts, string outputPath)
    {
        // Подготовка статистики, чтобы не рассчитывать в хэндлерах
        var stats = ReportStats.Build(ctx, opts);

        var doc = Document.Create(c =>
        {
            c.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18, Unit.Millimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontFamily("Segoe UI").FontSize(10));

                page.Header().Element(h => BuildHeader(h, opts, stats));
                page.Content().Element(content =>
                {
                    content.Column(col =>
                    {
                        col.Spacing(10);
                        col.Item().Element(e => BuildSummary(e, stats));

                        if (opts.IncludeSchedule && ctx.Sections.Any(s => s.Items.Count > 0))
                            col.Item().Element(e => BuildSchedule(e, ctx));

                        if (opts.IncludeHistory && stats.HistoryDays.Count > 0)
                            col.Item().Element(e => BuildHistory(e, stats));

                        if (opts.IncludePressure && stats.PressureEntries.Count > 0)
                            col.Item().Element(e => BuildPressure(e, stats));

                        if (opts.IncludeFinance && stats.Purchases.Count > 0)
                            col.Item().Element(e => BuildFinance(e, ctx, stats));
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(ColorMuted));
                    t.Span(Loc.T("pdf_footer_generated") + " ");
                    t.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                    t.Span("   ·   ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        doc.GeneratePdf(outputPath);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Шапка и сводка
    // ────────────────────────────────────────────────────────────────────

    private static void BuildHeader(IContainer container, ReportOptions opts, ReportStats stats)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(Loc.T("pdf_title"))
                .FontSize(18).Bold().FontColor(ColorPrimary);

            col.Item().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(11).FontColor(ColorH2Text));
                if (!string.IsNullOrWhiteSpace(opts.PatientName))
                {
                    t.Span(opts.PatientName).Bold();
                    t.Span("    ·    ");
                }
                t.Span(Loc.T("pdf_period",
                    ("from", Loc.FormatDateLong(opts.From)),
                    ("to",   Loc.FormatDateLong(opts.To))));
            });

            col.Item().PaddingTop(4).LineHorizontal(0.6f).LineColor(ColorBorder);
        });
    }

    private static void BuildSummary(IContainer container, ReportStats stats)
    {
        container.Column(col =>
        {
            col.Item().Text(Loc.T("pdf_section_summary"))
                .FontSize(13).Bold().FontColor(ColorH1Text);

            col.Item().PaddingTop(4).Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem().Element(e =>
                    StatCard(e, Loc.T("pdf_stat_days_full"),
                        stats.FullDays.ToString(), ColorSuccess));
                row.RelativeItem().Element(e =>
                    StatCard(e, Loc.T("pdf_stat_days_partial"),
                        stats.PartialDays.ToString(), ColorWarning));
                row.RelativeItem().Element(e =>
                    StatCard(e, Loc.T("pdf_stat_days_empty"),
                        stats.EmptyDays.ToString(), ColorDanger));
                row.RelativeItem().Element(e =>
                    StatCard(e, Loc.T("pdf_stat_overall"),
                        stats.OverallPercentText, ColorPrimary));
            });
        });
    }

    private static void StatCard(IContainer c, string label, string value, string color)
    {
        c.Background(ColorBgRow).Padding(8).Column(col =>
        {
            col.Item().Text(value).FontSize(18).Bold().FontColor(color);
            col.Item().Text(label).FontSize(9).FontColor(ColorMuted);
        });
    }

    // ────────────────────────────────────────────────────────────────────
    //  Список препаратов
    // ────────────────────────────────────────────────────────────────────

    private static void BuildSchedule(IContainer container, MedAppContext ctx)
    {
        container.Column(col =>
        {
            col.Item().Text(Loc.T("pdf_section_schedule"))
                .FontSize(13).Bold().FontColor(ColorH1Text);

            foreach (var section in ctx.Sections.Where(s => s.Items.Count > 0))
            {
                col.Item().PaddingTop(6).Text(section.Title)
                    .FontSize(11).Bold().FontColor(ColorH2Text);

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(50);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });

                    t.Header(h =>
                    {
                        HeaderCell(h.Cell(), Loc.T("pdf_col_time"));
                        HeaderCell(h.Cell(), Loc.T("pdf_col_med"));
                        HeaderCell(h.Cell(), Loc.T("pdf_col_doctor"));
                        HeaderCell(h.Cell(), Loc.T("pdf_col_course"));
                    });

                    foreach (var med in section.Items)
                    {
                        BodyCell(t.Cell(), med.Time);
                        t.Cell().Padding(4).Column(cc =>
                        {
                            cc.Item().Text(med.Name).FontSize(10).Bold();
                            if (!string.IsNullOrEmpty(med.Subtitle))
                                cc.Item().Text(med.Subtitle).FontSize(8).FontColor(ColorMuted);
                            if (!string.IsNullOrEmpty(med.Note))
                                cc.Item().Text(med.Note).FontSize(8).FontColor(ColorMuted);
                        });
                        BodyCell(t.Cell(), med.Doctor ?? "");
                        BodyCell(t.Cell(), med.Course ?? "");
                    }
                });
            }
        });
    }

    // ────────────────────────────────────────────────────────────────────
    //  История приёма
    // ────────────────────────────────────────────────────────────────────

    private static void BuildHistory(IContainer container, ReportStats stats)
    {
        container.Column(col =>
        {
            col.Item().Text(Loc.T("pdf_section_history"))
                .FontSize(13).Bold().FontColor(ColorH1Text);

            col.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(110);
                    c.ConstantColumn(70);
                    c.ConstantColumn(70);
                    c.RelativeColumn(); // bar
                });

                t.Header(h =>
                {
                    HeaderCell(h.Cell(), Loc.T("pdf_col_date"));
                    HeaderCell(h.Cell(), Loc.T("pdf_col_taken"));
                    HeaderCell(h.Cell(), Loc.T("pdf_col_percent"));
                    HeaderCell(h.Cell(), Loc.T("pdf_col_visual"));
                });

                int rowIdx = 0;
                foreach (var d in stats.HistoryDays)
                {
                    var bg = rowIdx++ % 2 == 0 ? ColorBgRow : "FFFFFF";

                    var date = d.Date;
                    var dow  = Loc.WeekdayShort(((int)date.DayOfWeek + 6) % 7);
                    t.Cell().Background(bg).Padding(4).Text($"{date:yyyy-MM-dd}  ·  {dow}").FontSize(9);
                    t.Cell().Background(bg).Padding(4).Text($"{d.Taken} / {d.Total}").FontSize(9);
                    t.Cell().Background(bg).Padding(4).Text(d.PercentText)
                        .FontSize(9).FontColor(ColorForPercent(d.Percent));

                    t.Cell().Background(bg).Padding(4).AlignMiddle().Element(cell =>
                    {
                        // Прогресс-бар: row из двух RelativeItem
                        cell.Height(8).Row(rr =>
                        {
                            var p = (float)Math.Max(0.001, Math.Min(1.0, d.Percent / 100.0));
                            rr.RelativeItem(p).Background(ColorForPercent(d.Percent));
                            rr.RelativeItem(Math.Max(0.001f, 1f - p)).Background(ColorBgRow);
                        });
                    });
                }
            });

            // Сводка по препаратам в выбранном периоде
            if (stats.MedTakenCounts.Count > 0)
            {
                col.Item().PaddingTop(8).Text(Loc.T("pdf_history_per_med"))
                    .FontSize(11).Bold().FontColor(ColorH2Text);

                col.Item().PaddingTop(4).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.ConstantColumn(70);
                        c.ConstantColumn(70);
                    });

                    t.Header(h =>
                    {
                        HeaderCell(h.Cell(), Loc.T("pdf_col_med"));
                        HeaderCell(h.Cell(), Loc.T("pdf_col_taken"));
                        HeaderCell(h.Cell(), Loc.T("pdf_col_percent"));
                    });

                    int idx = 0;
                    foreach (var (name, taken, total) in stats.MedTakenCounts)
                    {
                        var bg = idx++ % 2 == 0 ? ColorBgRow : "FFFFFF";
                        var pct = total == 0 ? 0 : 100.0 * taken / total;
                        t.Cell().Background(bg).Padding(4).Text(name).FontSize(9);
                        t.Cell().Background(bg).Padding(4).Text($"{taken} / {total}").FontSize(9);
                        t.Cell().Background(bg).Padding(4)
                            .Text($"{pct:0}%").FontSize(9).FontColor(ColorForPercent(pct));
                    }
                });
            }
        });
    }

    // ────────────────────────────────────────────────────────────────────
    //  АД
    // ────────────────────────────────────────────────────────────────────

    private static void BuildPressure(IContainer container, ReportStats stats)
    {
        container.Column(col =>
        {
            col.Item().Text(Loc.T("pdf_section_pressure"))
                .FontSize(13).Bold().FontColor(ColorH1Text);

            // Сводка средних
            if (stats.PressureEntries.Count > 0)
            {
                col.Item().PaddingTop(4).Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(e =>
                        StatCard(e, Loc.T("pdf_pressure_avg_sys"),
                            $"{stats.AvgSys:0}", ColorPrimary));
                    row.RelativeItem().Element(e =>
                        StatCard(e, Loc.T("pdf_pressure_avg_dia"),
                            $"{stats.AvgDia:0}", ColorPrimary));
                    row.RelativeItem().Element(e =>
                        StatCard(e, Loc.T("pdf_pressure_avg_pulse"),
                            stats.AvgPulse.HasValue ? $"{stats.AvgPulse:0}" : "—", ColorPrimary));
                    row.RelativeItem().Element(e =>
                        StatCard(e, Loc.T("pdf_pressure_count"),
                            stats.PressureEntries.Count.ToString(), ColorH1Text));
                });
            }

            col.Item().PaddingTop(6).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(110);
                    c.ConstantColumn(60);
                    c.ConstantColumn(60);
                    c.ConstantColumn(60);
                    c.RelativeColumn();
                });

                t.Header(h =>
                {
                    HeaderCell(h.Cell(), Loc.T("pdf_col_when"));
                    HeaderCell(h.Cell(), Loc.T("pressure_sys"));
                    HeaderCell(h.Cell(), Loc.T("pressure_dia"));
                    HeaderCell(h.Cell(), Loc.T("pressure_pulse"));
                    HeaderCell(h.Cell(), Loc.T("pdf_pressure_category"));
                });

                int idx = 0;
                foreach (var e in stats.PressureEntries
                    .OrderByDescending(x => x.ParsedTimestamp))
                {
                    var bg = idx++ % 2 == 0 ? ColorBgRow : "FFFFFF";
                    var (catText, catColor) = ClassifyBp(e.Systolic, e.Diastolic);

                    t.Cell().Background(bg).Padding(4).Text(e.Timestamp).FontSize(9);
                    t.Cell().Background(bg).Padding(4).Text(e.Systolic.ToString()).FontSize(9);
                    t.Cell().Background(bg).Padding(4).Text(e.Diastolic.ToString()).FontSize(9);
                    t.Cell().Background(bg).Padding(4)
                        .Text(e.Pulse.HasValue ? e.Pulse.ToString()! : "—").FontSize(9);
                    t.Cell().Background(bg).Padding(4)
                        .Text(catText).FontSize(9).FontColor(catColor);
                }
            });
        });
    }

    // ────────────────────────────────────────────────────────────────────
    //  Финансы
    // ────────────────────────────────────────────────────────────────────

    private static void BuildFinance(IContainer container, MedAppContext ctx, ReportStats stats)
    {
        container.Column(col =>
        {
            col.Item().Text(Loc.T("pdf_section_finance"))
                .FontSize(13).Bold().FontColor(ColorH1Text);

            col.Item().PaddingTop(4).Background(ColorBgRow).Padding(8).Row(row =>
            {
                row.RelativeItem().Text(Loc.T("finance_total"))
                    .FontSize(11).FontColor(ColorMuted);
                row.ConstantItem(120).AlignRight().Text(stats.GrandTotal.ToString("0.##", CultureInfo.InvariantCulture))
                    .FontSize(14).Bold().FontColor(ColorPrimary);
            });

            // Сводка по препаратам
            if (stats.PurchasesByMed.Count > 0)
            {
                col.Item().PaddingTop(8).Text(Loc.T("finance_per_med"))
                    .FontSize(11).Bold().FontColor(ColorH2Text);

                col.Item().PaddingTop(4).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.ConstantColumn(60);
                        c.ConstantColumn(80);
                    });

                    t.Header(h =>
                    {
                        HeaderCell(h.Cell(), Loc.T("pdf_col_med"));
                        HeaderCell(h.Cell(), Loc.T("pdf_col_count"));
                        HeaderCell(h.Cell(), Loc.T("pdf_col_amount"));
                    });

                    int idx = 0;
                    foreach (var (name, count, total) in stats.PurchasesByMed)
                    {
                        var bg = idx++ % 2 == 0 ? ColorBgRow : "FFFFFF";
                        t.Cell().Background(bg).Padding(4).Text(name).FontSize(9);
                        t.Cell().Background(bg).Padding(4).Text(count.ToString()).FontSize(9);
                        t.Cell().Background(bg).Padding(4)
                            .Text(total.ToString("0.##", CultureInfo.InvariantCulture)).FontSize(9);
                    }
                });
            }

            // Подробный журнал
            col.Item().PaddingTop(8).Text(Loc.T("finance_purchases"))
                .FontSize(11).Bold().FontColor(ColorH2Text);

            col.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(85);
                    c.RelativeColumn(3);
                    c.ConstantColumn(80);
                });

                t.Header(h =>
                {
                    HeaderCell(h.Cell(), Loc.T("pdf_col_date"));
                    HeaderCell(h.Cell(), Loc.T("pdf_col_med"));
                    HeaderCell(h.Cell(), Loc.T("pdf_col_amount"));
                });

                int idx = 0;
                foreach (var p in stats.Purchases.OrderByDescending(x => x.Date))
                {
                    var bg = idx++ % 2 == 0 ? ColorBgRow : "FFFFFF";
                    var name = stats.MedNameById.TryGetValue(p.MedId, out var n) ? n : p.MedId;
                    t.Cell().Background(bg).Padding(4).Text(p.Date).FontSize(9);
                    t.Cell().Background(bg).Padding(4).Column(cc =>
                    {
                        cc.Item().Text(name).FontSize(9);
                        if (!string.IsNullOrEmpty(p.Note))
                            cc.Item().Text(p.Note).FontSize(8).FontColor(ColorMuted);
                    });
                    t.Cell().Background(bg).Padding(4)
                        .Text(p.Amount.ToString("0.##", CultureInfo.InvariantCulture))
                        .FontSize(9).Bold().FontColor(ColorSuccess);
                }
            });
        });
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────────────

    private static void HeaderCell(IContainer c, string text)
        => c.Background(ColorH2Text).Padding(4)
            .Text(text).FontSize(9).Bold().FontColor("FFFFFF");

    private static void BodyCell(IContainer c, string text)
        => c.BorderBottom(0.4f).BorderColor(ColorBorder).Padding(4)
            .Text(text).FontSize(9);

    private static string ColorForPercent(double p) => p switch
    {
        >= 95 => ColorSuccess,
        >= 50 => ColorWarning,
        _     => ColorDanger,
    };

    /// <summary>Классификация АД по упрощённой шкале JNC-7 (для пометки).</summary>
    private static (string text, string color) ClassifyBp(int sys, int dia)
    {
        if (sys >= 180 || dia >= 110) return (Loc.T("pdf_bp_crisis"),  ColorDanger);
        if (sys >= 160 || dia >= 100) return (Loc.T("pdf_bp_stage2"),  ColorDanger);
        if (sys >= 140 || dia >= 90)  return (Loc.T("pdf_bp_stage1"),  ColorWarning);
        if (sys >= 130 || dia >= 85)  return (Loc.T("pdf_bp_high_normal"), ColorWarning);
        if (sys >= 120 || dia >= 80)  return (Loc.T("pdf_bp_normal_high"), ColorSuccess);
        return (Loc.T("pdf_bp_normal"), ColorSuccess);
    }
}

/// <summary>
/// Все агрегаты для отчёта собираем заранее, чтобы не считать в QuestPDF-handler-ах
/// (там это ломает рендер при многостраничных секциях).
/// </summary>
internal class ReportStats
{
    public List<DayStat> HistoryDays { get; init; } = new();
    public int FullDays { get; init; }
    public int PartialDays { get; init; }
    public int EmptyDays { get; init; }
    public double OverallPercent { get; init; }
    public string OverallPercentText => $"{OverallPercent:0}%";

    public List<(string Name, int Taken, int Total)> MedTakenCounts { get; init; } = new();

    public List<PressureEntry> PressureEntries { get; init; } = new();
    public double AvgSys { get; init; }
    public double AvgDia { get; init; }
    public double? AvgPulse { get; init; }

    public List<PurchaseEntry> Purchases { get; init; } = new();
    public List<(string Name, int Count, decimal Total)> PurchasesByMed { get; init; } = new();
    public decimal GrandTotal { get; init; }
    public Dictionary<string, string> MedNameById { get; init; } = new();

    public class DayStat
    {
        public DateTime Date { get; init; }
        public int Taken { get; init; }
        public int Total { get; init; }
        public double Percent => Total == 0 ? 0 : 100.0 * Taken / Total;
        public string PercentText => $"{Percent:0}%";
    }

    public static ReportStats Build(MedAppContext ctx, ReportOptions opts)
    {
        var medById = ctx.Sections.SelectMany(s => s.Items).ToDictionary(m => m.Id, m => m);
        var totalIds = medById.Count;

        // ── История
        var days = new List<DayStat>();
        int full = 0, partial = 0, empty = 0;
        var medTaken = new Dictionary<string, int>();
        var medSeenDays = new Dictionary<string, int>();

        for (var d = opts.From.Date; d <= opts.To.Date; d = d.AddDays(1))
        {
            var iso = d.ToString("yyyy-MM-dd");
            var taken = ctx.State.CountTaken(iso);
            var hasData = ctx.State.HasAnyMarks(iso);

            days.Add(new DayStat { Date = d, Taken = taken, Total = totalIds });

            if (!hasData) empty++;
            else if (taken >= totalIds && totalIds > 0) full++;
            else if (taken == 0) empty++;
            else partial++;

            // По препаратам — считаем только дни, на которые в state есть запись
            // (иначе препарат, добавленный недавно, выглядит «сильно пропущенным»).
            if (hasData)
            {
                foreach (var m in medById.Values)
                {
                    if (!medSeenDays.ContainsKey(m.Id)) medSeenDays[m.Id] = 0;
                    medSeenDays[m.Id] = medSeenDays[m.Id] + 1;
                    if (ctx.State.IsTaken(iso, m.Id))
                    {
                        if (!medTaken.ContainsKey(m.Id)) medTaken[m.Id] = 0;
                        medTaken[m.Id] = medTaken[m.Id] + 1;
                    }
                }
            }
        }

        var medCounts = medById.Values
            .Select(m => (m.Name,
                          medTaken.TryGetValue(m.Id, out var v) ? v : 0,
                          medSeenDays.TryGetValue(m.Id, out var s) ? s : 0))
            .Where(x => x.Item3 > 0)
            .OrderByDescending(x => x.Item3 == 0 ? 0 : 100.0 * x.Item2 / x.Item3)
            .ToList();

        double overall = (full + partial * 0.5) / Math.Max(1, days.Count) * 100.0;

        // ── Давление
        var pressure = ctx.State.PressureLog
            .Where(p =>
            {
                var dt = p.ParsedTimestamp;
                return dt != DateTime.MinValue
                    && dt.Date >= opts.From.Date
                    && dt.Date <= opts.To.Date;
            })
            .ToList();
        var avgSys = pressure.Count == 0 ? 0 : pressure.Average(p => (double)p.Systolic);
        var avgDia = pressure.Count == 0 ? 0 : pressure.Average(p => (double)p.Diastolic);
        double? avgPulse = null;
        var withPulse = pressure.Where(p => p.Pulse.HasValue).Select(p => (double)p.Pulse!.Value).ToList();
        if (withPulse.Count > 0) avgPulse = withPulse.Average();

        // ── Финансы
        var purchases = ctx.State.Purchases
            .Where(p =>
            {
                if (!DateTime.TryParse(p.Date, out var d)) return false;
                return d.Date >= opts.From.Date && d.Date <= opts.To.Date;
            })
            .ToList();

        var byMed = purchases
            .GroupBy(p => p.MedId)
            .Select(g =>
            {
                var n = medById.TryGetValue(g.Key, out var m) ? m.Name : g.Key;
                return (n, g.Count(), g.Sum(p => p.Amount));
            })
            .OrderByDescending(x => x.Item3)
            .ToList();

        var grand = purchases.Sum(p => p.Amount);

        return new ReportStats
        {
            HistoryDays = days,
            FullDays = full,
            PartialDays = partial,
            EmptyDays = empty,
            OverallPercent = overall,
            MedTakenCounts = medCounts,

            PressureEntries = pressure,
            AvgSys = avgSys,
            AvgDia = avgDia,
            AvgPulse = avgPulse,

            Purchases = purchases,
            PurchasesByMed = byMed,
            GrandTotal = grand,
            MedNameById = medById.ToDictionary(kv => kv.Key, kv => kv.Value.Name),
        };
    }
}
