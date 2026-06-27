using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TakingMedications.Common;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class HistoryView : UserControl
{
    private static readonly Color CalFull    = (Color)ColorConverter.ConvertFromString("#2ECC71"); // green
    private static readonly Color CalPartial = (Color)ColorConverter.ConvertFromString("#F1C40F"); // yellow
    private static readonly Color CalEmpty   = (Color)ColorConverter.ConvertFromString("#E74C3C"); // red
    private static readonly Color CalNone    = (Color)ColorConverter.ConvertFromString("#3D4257"); // grey

    private MedAppContext? _ctx;
    private DateTime _viewMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? _selectedDay = DateTime.Today;

    public HistoryView()
    {
        InitializeComponent();
        Loc.LanguageChanged += Render;
        ThemeService.ThemeChanged += Render;
        Unloaded += (_, _) =>
        {
            Loc.LanguageChanged    -= Render;
            ThemeService.ThemeChanged -= Render;
        };
    }

    public void Initialize(MedAppContext ctx)
    {
        _ctx = ctx;
        _ctx.DataChanged += Render;
        Render();
    }

    public void Render()
    {
        if (_ctx == null) return;

        ApplyTabHeaders();
        MonthLabel.Text = $"{Loc.MonthNominative(_viewMonth.Month)} {_viewMonth.Year}";
        BuildWeekdaysHeader();
        BuildCalendarGrid();
        BuildLegend();
        RenderDayDetails();
    }

    private void BuildWeekdaysHeader()
    {
        WeekdaysHeader.Children.Clear();
        WeekdaysHeader.ColumnDefinitions.Clear();
        for (int i = 0; i < 7; i++)
            WeekdaysHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < 7; i++)
        {
            var t = new TextBlock
            {
                Text = Loc.WeekdayShort(i),
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(t, i);
            WeekdaysHeader.Children.Add(t);
        }
    }

    private void BuildCalendarGrid()
    {
        if (_ctx == null) return;

        CalendarGrid.Children.Clear();
        CalendarGrid.ColumnDefinitions.Clear();
        CalendarGrid.RowDefinitions.Clear();
        for (int i = 0; i < 7; i++)
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var first = new DateTime(_viewMonth.Year, _viewMonth.Month, 1);
        int firstDow = ((int)first.DayOfWeek + 6) % 7; // Mon=0
        int days = DateTime.DaysInMonth(_viewMonth.Year, _viewMonth.Month);

        int rows = (int)Math.Ceiling((firstDow + days) / 7.0);
        for (int r = 0; r < rows; r++)
            CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var totalIds = _ctx.Sections.Sum(s => s.Items.Count);

        for (int day = 1; day <= days; day++)
        {
            int idx = firstDow + day - 1;
            int row = idx / 7, col = idx % 7;
            var d = new DateTime(_viewMonth.Year, _viewMonth.Month, day);
            var iso = d.ToString("yyyy-MM-dd");

            var cell = BuildDayCell(d, iso, totalIds);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            CalendarGrid.Children.Add(cell);
        }
    }

    private Border BuildDayCell(DateTime d, string iso, int totalIds)
    {
        var taken = _ctx!.State.CountTaken(iso);
        var hasData = _ctx.State.HasAnyMarks(iso);

        Color fill;
        if (!hasData)            fill = CalNone;
        else if (taken == 0)     fill = CalEmpty;
        else if (taken >= totalIds && totalIds > 0) fill = CalFull;
        else                     fill = CalPartial;

        var isSelected = _selectedDay.HasValue
            && _selectedDay.Value.Date == d.Date;
        var isToday = d.Date == DateTime.Today;

        var dayLabel = new TextBlock
        {
            Text = d.Day.ToString(),
            FontSize = 14,
            FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var border = new Border
        {
            Background = new SolidColorBrush(fill),
            Margin = new Thickness(2),
            Padding = new Thickness(0),
            MinHeight = 44,
            CornerRadius = new CornerRadius(8),
            BorderBrush = isSelected
                ? (Brush)FindResource("AccentBrush")
                : (isToday ? Brushes.White : Brushes.Transparent),
            BorderThickness = new Thickness(isSelected ? 3 : (isToday ? 2 : 0)),
            Cursor = Cursors.Hand,
            Child = dayLabel,
            Tag = d
        };
        border.MouseLeftButtonUp += (_, _) =>
        {
            _selectedDay = d;
            Render();
        };
        return border;
    }

    private void BuildLegend()
    {
        LegendPanel.Children.Clear();
        AddLegendItem(CalFull,    Loc.T("history_legend_full"));
        AddLegendItem(CalPartial, Loc.T("history_legend_partial"));
        AddLegendItem(CalEmpty,   Loc.T("history_legend_empty"));
        AddLegendItem(CalNone,    Loc.T("history_legend_none"));
    }

    private void AddLegendItem(Color c, string text)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 0) };
        sp.Children.Add(new Border
        {
            Width = 14, Height = 14,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(c),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        sp.Children.Add(new TextBlock
        {
            Text = text, FontSize = 12,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        LegendPanel.Children.Add(sp);
    }

    private void RenderDayDetails()
    {
        DayDetailsPanel.Children.Clear();
        if (_ctx == null || _selectedDay == null)
        {
            DayDetailsPanel.Children.Add(new TextBlock
            {
                Text = Loc.T("history_select_day"),
                FontSize = 13,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        var d = _selectedDay.Value;
        var iso = d.ToString("yyyy-MM-dd");

        var header = new TextBlock
        {
            Text = $"{Loc.FormatDateLong(d)}  ·  {Loc.WeekdayFull(((int)d.DayOfWeek + 6) % 7)}",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        DayDetailsPanel.Children.Add(header);

        var totalIds = _ctx.Sections.Sum(s => s.Items.Count);
        var taken = _ctx.State.CountTaken(iso);

        DayDetailsPanel.Children.Add(new TextBlock
        {
            Text = Loc.T("history_day_taken", ("taken", taken), ("total", totalIds)),
            FontSize = 13,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            Margin = new Thickness(0, 0, 0, 10)
        });

        if (!_ctx.State.HasAnyMarks(iso))
        {
            DayDetailsPanel.Children.Add(new TextBlock
            {
                Text = Loc.T("history_day_no_data", ("date", Loc.FormatDateLong(d))),
                FontSize = 12,
                Foreground = (Brush)FindResource("TextSecondaryBrush")
            });
            return;
        }

        foreach (var section in _ctx.Sections)
        {
            if (section.Items.Count == 0) continue;
            DayDetailsPanel.Children.Add(new TextBlock
            {
                Text = section.Title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 8, 0, 4)
            });
            foreach (var med in section.Items)
            {
                var t = _ctx.State.IsTaken(iso, med.Id);
                var line = new StackPanel { Orientation = Orientation.Horizontal };
                line.Children.Add(new TextBlock
                {
                    Text = t ? "✅ " : "⬜ ",
                    FontSize = 13, Margin = new Thickness(0, 0, 4, 0)
                });
                line.Children.Add(new TextBlock
                {
                    Text = $"{med.Time}  {med.Name}",
                    FontSize = 13,
                    Foreground = (Brush)FindResource("TextPrimaryBrush")
                });
                DayDetailsPanel.Children.Add(line);
            }
        }
    }

    private void BtnPrevMonth_Click(object sender, RoutedEventArgs e)
    { _viewMonth = _viewMonth.AddMonths(-1); Render(); }

    private void BtnNextMonth_Click(object sender, RoutedEventArgs e)
    { _viewMonth = _viewMonth.AddMonths( 1); Render(); }

    // ── Вкладки ───────────────────────────────────────────────────────

    private void HistoryTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyTabHeaders();
        if (HistoryTabs.SelectedItem == TabCharts)
        {
            DrawAdherenceChart();
            DrawPressureChart();
        }
    }

    private void ApplyTabHeaders()
    {
        TabCalendar.Header = Loc.T("history_tab_calendar");
        TabCharts.Header   = Loc.T("history_tab_charts");
    }

    // ── Графики ───────────────────────────────────────────────────────

    private void AdherenceChart_SizeChanged(object sender, SizeChangedEventArgs e)
        => DrawAdherenceChart();

    private void PressureChart_SizeChanged(object sender, SizeChangedEventArgs e)
        => DrawPressureChart();

    /// <summary>Столбчатый график приёма лекарств по месяцам (последние 6).</summary>
    private void DrawAdherenceChart()
    {
        AdherenceChart.Children.Clear();
        if (_ctx == null) return;

        LblChartAdherence.Text = Loc.T("history_chart_adherence");

        double w = AdherenceChart.ActualWidth;
        double h = AdherenceChart.ActualHeight;
        if (w < 40 || h < 40) return;

        // Собираем данные за последние 6 месяцев
        int totalMeds = _ctx.Sections.Sum(s => s.Items.Count);
        if (totalMeds == 0) return;

        var months = new List<(DateTime month, double pct, string label)>();
        for (int i = 5; i >= 0; i--)
        {
            var m = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-i);
            int days = DateTime.DaysInMonth(m.Year, m.Month);
            int daysWithData = 0, totalTaken = 0, totalPossible = 0;
            for (int d = 1; d <= days; d++)
            {
                var iso = new DateTime(m.Year, m.Month, d).ToString("yyyy-MM-dd");
                if (!_ctx.State.HasAnyMarks(iso)) continue;
                daysWithData++;
                totalTaken    += _ctx.State.CountTaken(iso);
                totalPossible += totalMeds;
            }
            double pct = totalPossible > 0 ? totalTaken * 100.0 / totalPossible : -1;
            months.Add((m, pct, Loc.MonthGenitive(m.Month)[..3]));
        }

        const double padL = 36, padR = 10, padT = 10, padB = 28;
        double plotW = w - padL - padR;
        double plotH = h - padT - padB;
        double barW  = plotW / months.Count * 0.65;
        double gap   = plotW / months.Count;

        // Сетка Y
        foreach (var level in new[] { 25, 50, 75, 100 })
        {
            double y = padT + plotH * (1 - level / 100.0);
            AddLine(AdherenceChart, padL, y, padL + plotW, y,
                new SolidColorBrush(Color.FromArgb(40, 160, 160, 200)), 1, isDashed: true);
            AddText(AdherenceChart, $"{level}%", padL - 4, y,
                new SolidColorBrush(Color.FromArgb(160, 160, 160, 200)), 9, TextAlignment.Right);
        }

        // Столбцы
        for (int i = 0; i < months.Count; i++)
        {
            var (_, pct, label) = months[i];
            double x = padL + i * gap + (gap - barW) / 2;

            if (pct >= 0)
            {
                double barH = plotH * pct / 100.0;
                double barY = padT + plotH - barH;

                Color col = pct >= 90 ? Color.FromRgb(0x2E, 0xCC, 0x71)
                           : pct >= 60 ? Color.FromRgb(0xF1, 0xC4, 0x0F)
                           : Color.FromRgb(0xE7, 0x4C, 0x3C);

                var rect = new Rectangle
                {
                    Width  = barW, Height = Math.Max(2, barH),
                    Fill   = new SolidColorBrush(col),
                    RadiusX = 4, RadiusY = 4
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect,  barY);
                AdherenceChart.Children.Add(rect);

                // % над столбцом
                AddText(AdherenceChart, $"{pct:0}%", x + barW / 2, barY - 14,
                    new SolidColorBrush(Color.FromRgb(0xEA, 0xEA, 0xEA)), 9, TextAlignment.Center);
            }

            // Метка месяца
            AddText(AdherenceChart, label, x + barW / 2, padT + plotH + 6,
                new SolidColorBrush(Color.FromArgb(180, 160, 160, 200)), 9, TextAlignment.Center);
        }
    }

    /// <summary>Линейный график АД за последние 30 измерений.</summary>
    private void DrawPressureChart()
    {
        PressureChart.Children.Clear();
        if (_ctx == null) return;

        LblChartPressure.Text = Loc.T("history_chart_pressure");

        double w = PressureChart.ActualWidth;
        double h = PressureChart.ActualHeight;
        if (w < 40 || h < 40) return;

        var entries = _ctx.State.PressureLog
            .Where(e => e.ParsedTimestamp != DateTime.MinValue)
            .OrderBy(e => e.ParsedTimestamp)
            .TakeLast(30).ToList();

        if (entries.Count < 2)
        {
            AddText(PressureChart, Loc.T("pressure_chart_no_data"), w / 2, h / 2,
                new SolidColorBrush(Color.FromArgb(160, 160, 160, 200)), 12, TextAlignment.Center, center: true);
            return;
        }

        const double padL = 42, padR = 16, padT = 10, padB = 30;
        double plotW = w - padL - padR;
        double plotH = h - padT - padB;

        int yMin = Math.Max(40,  entries.Min(e => Math.Min(e.Diastolic, e.Pulse ?? 999)) - 10);
        int yMax = Math.Min(220, entries.Max(e => e.Systolic) + 15);
        if (yMax - yMin < 40) yMax = yMin + 40;

        double ToX(int i) => padL + i * plotW / (entries.Count - 1);
        double ToY(double v) => padT + plotH - (v - yMin) * plotH / (yMax - yMin);

        foreach (var lv in new[] { 80, 90, 120, 130, 140, 160 }.Where(v => v >= yMin && v <= yMax))
        {
            double y = ToY(lv);
            AddLine(PressureChart, padL, y, padL + plotW, y,
                new SolidColorBrush(Color.FromArgb(40, 160, 160, 200)), 1, isDashed: true);
            AddText(PressureChart, lv.ToString(), padL - 4, y,
                new SolidColorBrush(Color.FromArgb(160, 160, 160, 200)), 9, TextAlignment.Right);
        }

        DrawPolyline(PressureChart, entries, i => ToX(i), e => ToY(e.Systolic),
            new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)), 2.5);
        DrawPolyline(PressureChart, entries, i => ToX(i), e => ToY(e.Diastolic),
            new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)), 2.5);

        // Точки
        for (int i = 0; i < entries.Count; i++)
        {
            AddDot(PressureChart, ToX(i), ToY(entries[i].Systolic),
                new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)), 4);
            AddDot(PressureChart, ToX(i), ToY(entries[i].Diastolic),
                new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)), 3);
        }

        // Даты X
        int step = Math.Max(1, entries.Count / 6);
        for (int i = 0; i < entries.Count; i += step)
        {
            var ts = entries[i].Timestamp;
            AddText(PressureChart, ts.Length >= 10 ? ts[5..10] : ts,
                ToX(i), padT + plotH + 6,
                new SolidColorBrush(Color.FromArgb(160, 160, 160, 200)), 9, TextAlignment.Center);
        }

        // Легенда
        AddLegendLine(PressureChart, padL + 4, padT + 4,
            Color.FromRgb(0xE7, 0x4C, 0x3C), Loc.T("pressure_chart_sys"));
        AddLegendLine(PressureChart, padL + 4, padT + 18,
            Color.FromRgb(0x34, 0x98, 0xDB), Loc.T("pressure_chart_dia"));
    }

    // ── Helpers для рисования ─────────────────────────────────────────

    private static void DrawPolyline(Canvas canvas, List<PressureEntry> pts,
        Func<int, double> fx, Func<PressureEntry, double> fy, Brush stroke, double thick)
    {
        var poly = new Polyline
        {
            Stroke = stroke, StrokeThickness = thick, StrokeLineJoin = PenLineJoin.Round
        };
        for (int i = 0; i < pts.Count; i++)
            poly.Points.Add(new Point(fx(i), fy(pts[i])));
        canvas.Children.Add(poly);
    }

    private static void AddDot(Canvas c, double cx, double cy, Brush fill, double r)
    {
        var el = new Ellipse { Width = r * 2, Height = r * 2, Fill = fill };
        Canvas.SetLeft(el, cx - r); Canvas.SetTop(el, cy - r);
        c.Children.Add(el);
    }

    private static void AddLine(Canvas c, double x1, double y1, double x2, double y2,
        Brush stroke, double thick, bool isDashed = false)
    {
        var line = new Line { X1=x1,Y1=y1,X2=x2,Y2=y2, Stroke=stroke, StrokeThickness=thick };
        if (isDashed) line.StrokeDashArray = new DoubleCollection { 4, 3 };
        c.Children.Add(line);
    }

    private static void AddText(Canvas c, string text, double x, double y,
        Brush fg, double size, TextAlignment align, bool center = false)
    {
        var tb = new TextBlock { Text=text, FontSize=size, Foreground=fg, TextAlignment=align };
        if (center) { tb.Width=c.ActualWidth; Canvas.SetLeft(tb,0); Canvas.SetTop(tb, y-size/2); }
        else { Canvas.SetLeft(tb, align==TextAlignment.Right ? x-36 : x-14); Canvas.SetTop(tb, y-size/2-1); if(align==TextAlignment.Right) tb.Width=34; }
        c.Children.Add(tb);
    }

    private static void AddLegendLine(Canvas c, double x, double y, Color col, string label)
    {
        var line = new Line { X1=x,Y1=y+5,X2=x+20,Y2=y+5,
            Stroke=new SolidColorBrush(col), StrokeThickness=2 };
        c.Children.Add(line);
        var tb = new TextBlock { Text=label, FontSize=10,
            Foreground=new SolidColorBrush(Color.FromArgb(200,200,200,220)) };
        Canvas.SetLeft(tb, x+24); Canvas.SetTop(tb, y);
        c.Children.Add(tb);
    }
}
