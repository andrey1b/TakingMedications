using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TakingMedications.Common;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class PressureDialog : Window
{
    private const int    SysMin   = 60,  SysMax   = 260;
    private const int    DiaMin   = 30,  DiaMax   = 180;
    private const int    PulseMin = 30,  PulseMax = 220;
    private const double SugarMin = 1.0, SugarMax = 30.0;

    private readonly MedAppContext _ctx;
    private Border?       _selectedRow;
    private PressureEntry? _selectedEntry;

    // цвета категорий
    private static readonly Color ColOptimal    = Color.FromRgb(0x2E, 0xCC, 0x71);
    private static readonly Color ColNormal     = Color.FromRgb(0x27, 0xAE, 0x60);
    private static readonly Color ColHighNormal = Color.FromRgb(0xF1, 0xC4, 0x0F);
    private static readonly Color ColStage1     = Color.FromRgb(0xE6, 0x7E, 0x22);
    private static readonly Color ColStage2     = Color.FromRgb(0xE7, 0x4C, 0x3C);

    public PressureDialog(MedAppContext ctx)
    {
        InitializeComponent();
        _ctx = ctx;
        ApplyLocalization();
        RenderHistory();
        Loaded += (_, _) => SysBox.Focus();
    }

    // ── Локализация ───────────────────────────────────────────────────────

    private void ApplyLocalization()
    {
        Title              = Loc.T("pressure_title");
        HeaderLabel.Text   = Loc.T("pressure_header", ("date", DateTime.Now.ToString("yyyy-MM-dd HH:mm")));
        LblPulse.Text      = Loc.T("pressure_col_pulse");
        LblSys.Text        = Loc.T("pressure_col_sys");
        LblDia.Text        = Loc.T("pressure_col_dia");
        LblSugar.Text      = Loc.T("pressure_col_sugar");
        BtnSave.Content    = Loc.T("pressure_save");
        BtnClose.Content   = Loc.T("btn_close");
        BtnDelete.Content  = Loc.T("pressure_delete_selected");

        TabHistory.Header  = Loc.T("pressure_tab_history");
        TabChart.Header    = Loc.T("pressure_tab_chart");

        LblLegOptimal.Text    = Loc.T("pressure_bp_optimal");
        LblLegNormal.Text     = Loc.T("pdf_bp_normal_high");
        LblLegHighNormal.Text = Loc.T("pdf_bp_high_normal");
        LblLegStage1.Text     = Loc.T("pdf_bp_stage1");
        LblLegStage2.Text     = Loc.T("pdf_bp_stage2");
    }

    // ── История ───────────────────────────────────────────────────────────

    private void RenderHistory()
    {
        HistoryPanel.Children.Clear();
        _selectedRow   = null;
        _selectedEntry = null;
        BtnDelete.IsEnabled = false;

        var entries = _ctx.State.PressureLog
            .OrderByDescending(e => e.ParsedTimestamp)
            .ToList();

        if (entries.Count == 0)
        {
            HistoryPanel.Children.Add(new TextBlock
            {
                Text       = Loc.T("pressure_no_records"),
                FontSize   = 13,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin     = new Thickness(8, 10, 8, 10)
            });
            return;
        }

        // Заголовок таблицы
        HistoryPanel.Children.Add(BuildTableHeader());

        foreach (var e in entries)
            HistoryPanel.Children.Add(BuildRow(e));
    }

    private Border BuildTableHeader()
    {
        var grid = MakeRowGrid();
        void Hdr(int col, string text, TextAlignment align = TextAlignment.Left)
        {
            var tb = new TextBlock
            {
                Text            = text,
                FontSize        = 11,
                FontWeight      = FontWeights.SemiBold,
                Foreground      = (Brush)FindResource("TextSecondaryBrush"),
                TextAlignment   = align,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }

        Hdr(0, Loc.T("pressure_col_datetime"));
        Hdr(1, Loc.T("pressure_col_sys"),  TextAlignment.Center);
        Hdr(2, Loc.T("pressure_col_dia"),  TextAlignment.Center);
        Hdr(3, Loc.T("pressure_col_pulse"), TextAlignment.Center);
        Hdr(4, Loc.T("pressure_col_sugar"), TextAlignment.Center);
        Hdr(5, Loc.T("pressure_col_category"));

        return new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = (Brush)FindResource("TextSecondaryBrush"),
            Child = grid
        };
    }

    private Border BuildRow(PressureEntry entry)
    {
        var (bpColor, catKey) = GetBpCategory(entry.Systolic, entry.Diastolic);
        var bpBrush = new SolidColorBrush(bpColor);

        var grid = MakeRowGrid();
        void Add(int col, UIElement el) { Grid.SetColumn(el, col); grid.Children.Add(el); }

        Add(0, new TextBlock
        {
            Text      = entry.Timestamp,
            FontSize  = 12,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        Add(1, Num(entry.Systolic.ToString(), bpBrush, TextAlignment.Center));
        Add(2, Num(entry.Diastolic.ToString(), bpBrush, TextAlignment.Center));
        Add(3, Num(
            entry.Pulse.HasValue ? $"❤ {entry.Pulse}" : "—",
            (Brush)FindResource("TextPrimaryBrush"),
            TextAlignment.Center));

        Brush sugarBrush;
        string sugarText;
        if (entry.Sugar.HasValue)
        {
            sugarText  = entry.Sugar.Value.ToString("F1", CultureInfo.InvariantCulture);
            sugarBrush = new SolidColorBrush(GetSugarColor(entry.Sugar.Value));
        }
        else
        {
            sugarText  = "—";
            sugarBrush = (Brush)FindResource("TextSecondaryBrush");
        }
        Add(4, Num(sugarText, sugarBrush, TextAlignment.Center));

        Add(5, new TextBlock
        {
            Text      = Loc.T(catKey),
            FontSize  = 12,
            Foreground = bpBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var row = new Border
        {
            Padding = new Thickness(8, 5, 8, 5),
            CornerRadius = new CornerRadius(6),
            Cursor  = System.Windows.Input.Cursors.Hand,
            Child   = grid,
            Tag     = entry
        };

        row.MouseLeftButtonDown += (_, _) => SelectRow(row, entry);

        return row;
    }

    private void SelectRow(Border row, PressureEntry entry)
    {
        if (_selectedRow != null)
            _selectedRow.Background = Brushes.Transparent;

        _selectedRow   = row;
        _selectedEntry = entry;
        row.Background = new SolidColorBrush(Color.FromArgb(60, 108, 99, 255));
        BtnDelete.IsEnabled = true;
    }

    private static Grid MakeRowGrid()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(135) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return g;
    }

    private static TextBlock Num(string text, Brush fg, TextAlignment align)
        => new()
        {
            Text      = text,
            FontSize  = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = fg,
            TextAlignment = align,
            VerticalAlignment = VerticalAlignment.Center
        };

    // ── Категории АД ─────────────────────────────────────────────────────

    private static (Color color, string locKey) GetBpCategory(int sys, int dia)
    {
        if (sys >= 180 || dia >= 110) return (ColStage2,     "pdf_bp_crisis");
        if (sys >= 160 || dia >= 100) return (ColStage2,     "pdf_bp_stage2");
        if (sys >= 140 || dia >= 90)  return (ColStage1,     "pdf_bp_stage1");
        if (sys >= 130 || dia >= 85)  return (ColHighNormal, "pdf_bp_high_normal");
        if (sys >= 120 || dia >= 80)  return (ColNormal,     "pdf_bp_normal_high");
        return (ColOptimal, "pressure_bp_optimal");
    }

    private static Color GetSugarColor(double sugar)
    {
        if (sugar < 3.0)  return ColStage2;
        if (sugar < 3.9)  return ColStage1;
        if (sugar <= 5.5) return ColOptimal;
        if (sugar <= 7.0) return ColHighNormal;
        return ColStage2;
    }

    // ── График ────────────────────────────────────────────────────────────

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();

        var entries = _ctx.State.PressureLog
            .Where(e => e.ParsedTimestamp != DateTime.MinValue)
            .OrderBy(e => e.ParsedTimestamp)
            .TakeLast(40)
            .ToList();

        double w = ChartCanvas.ActualWidth;
        double h = ChartCanvas.ActualHeight;
        if (w < 80 || h < 60 || entries.Count < 2)
        {
            if (entries.Count < 2)
                AddChartText(Loc.T("pressure_chart_no_data"), w / 2, h / 2,
                             (Brush)FindResource("TextSecondaryBrush"), 13, true);
            return;
        }

        const double padL = 42, padR = 16, padT = 16, padB = 40;
        double plotW = w - padL - padR;
        double plotH = h - padT - padB;

        // Диапазон Y
        int yMin = Math.Max(40,  entries.Min(e => Math.Min(e.Diastolic, e.Pulse ?? 999)) - 10);
        int yMax = Math.Min(220, entries.Max(e => e.Systolic) + 15);
        if (yMax - yMin < 40) { yMax = yMin + 40; }

        double ToX(int i)  => padL + i * plotW / (entries.Count - 1);
        double ToY(double v) => padT + plotH - (v - yMin) * plotH / (yMax - yMin);

        // Горизонтальные линии сетки на клинически значимых уровнях
        var gridLevels = new[] { 80, 90, 120, 130, 140, 160, 180 }
            .Where(v => v >= yMin && v <= yMax).ToList();

        foreach (var lv in gridLevels)
        {
            double y = ToY(lv);
            AddLine(padL, y, padL + plotW, y,
                    new SolidColorBrush(Color.FromArgb(40, 160, 160, 200)), 1, true);
            AddChartText(lv.ToString(), padL - 4, y,
                         new SolidColorBrush(Color.FromArgb(180, 160, 160, 200)),
                         10, false, TextAlignment.Right);
        }

        // Линия систолического
        DrawPolyline(entries, e => ToX(entries.IndexOf(e)), e => ToY(e.Systolic),
                     new SolidColorBrush(ColStage1), 2.5);

        // Линия диастолического
        DrawPolyline(entries, e => ToX(entries.IndexOf(e)), e => ToY(e.Diastolic),
                     new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)), 2.5);

        // Линия пульса (если есть)
        if (entries.Any(e => e.Pulse.HasValue))
        {
            var withPulse = entries.Where(e => e.Pulse.HasValue).ToList();
            DrawPolyline(withPulse,
                         e => ToX(entries.IndexOf(e)),
                         e => ToY(e.Pulse!.Value),
                         new SolidColorBrush(ColOptimal), 1.5, isDashed: true);
        }

        // Точки
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var (col, _) = GetBpCategory(e.Systolic, e.Diastolic);
            AddDot(ToX(i), ToY(e.Systolic), new SolidColorBrush(col), 5);
            AddDot(ToX(i), ToY(e.Diastolic), new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)), 4);
        }

        // Метки дат по X (каждые N точек)
        int step = Math.Max(1, entries.Count / 6);
        for (int i = 0; i < entries.Count; i += step)
        {
            var ts = entries[i].Timestamp;
            string label = ts.Length >= 10 ? ts[5..10] : ts; // MM-DD
            AddChartText(label, ToX(i), padT + plotH + 6,
                         new SolidColorBrush(Color.FromArgb(160, 160, 160, 200)),
                         10, false, TextAlignment.Center);
        }

        // Легенда
        double lx = padL + 6, ly = padT + 6;
        AddLegendItem(lx, ly,      ColStage1,                              Loc.T("pressure_chart_sys"));
        AddLegendItem(lx, ly + 18, Color.FromRgb(0x34, 0x98, 0xDB),       Loc.T("pressure_chart_dia"));
        AddLegendItem(lx, ly + 36, ColOptimal,                             Loc.T("pressure_chart_pulse"), isDashed: true);
    }

    private void DrawPolyline(IList<PressureEntry> pts,
                               Func<PressureEntry, double> fx,
                               Func<PressureEntry, double> fy,
                               Brush stroke, double thickness,
                               bool isDashed = false)
    {
        var poly = new Polyline
        {
            Stroke          = stroke,
            StrokeThickness = thickness,
            StrokeLineJoin  = PenLineJoin.Round
        };
        if (isDashed)
            poly.StrokeDashArray = new DoubleCollection { 4, 3 };

        foreach (var e in pts)
            poly.Points.Add(new Point(fx(e), fy(e)));

        ChartCanvas.Children.Add(poly);
    }

    private void AddLine(double x1, double y1, double x2, double y2,
                         Brush stroke, double thickness, bool isDashed = false)
    {
        var line = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = stroke, StrokeThickness = thickness
        };
        if (isDashed) line.StrokeDashArray = new DoubleCollection { 4, 3 };
        ChartCanvas.Children.Add(line);
    }

    private void AddDot(double cx, double cy, Brush fill, double r)
    {
        var el = new Ellipse
        {
            Width = r * 2, Height = r * 2,
            Fill  = fill,
            Stroke = Brushes.Black, StrokeThickness = 0.5
        };
        Canvas.SetLeft(el, cx - r);
        Canvas.SetTop(el,  cy - r);
        ChartCanvas.Children.Add(el);
    }

    private void AddChartText(string text, double x, double y,
                               Brush fg, double size,
                               bool center = false,
                               TextAlignment align = TextAlignment.Left)
    {
        var tb = new TextBlock
        {
            Text      = text,
            FontSize  = size,
            Foreground = fg,
            TextAlignment = align
        };
        if (center)
        {
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.Width = ChartCanvas.ActualWidth;
            Canvas.SetTop(tb, y - size / 2);
            Canvas.SetLeft(tb, 0);
        }
        else
        {
            Canvas.SetLeft(tb, align == TextAlignment.Right ? x - 38 : x - 14);
            Canvas.SetTop(tb,  y - size / 2 - 1);
            if (align == TextAlignment.Right) tb.Width = 36;
        }
        ChartCanvas.Children.Add(tb);
    }

    private void AddLegendItem(double x, double y, Color col, string label,
                                bool isDashed = false)
    {
        var line = new Line
        {
            X1 = x, Y1 = y + 6, X2 = x + 22, Y2 = y + 6,
            Stroke = new SolidColorBrush(col), StrokeThickness = 2
        };
        if (isDashed) line.StrokeDashArray = new DoubleCollection { 4, 3 };
        ChartCanvas.Children.Add(line);

        var tb = new TextBlock
        {
            Text      = label,
            FontSize  = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(200, 200, 200, 220))
        };
        Canvas.SetLeft(tb, x + 26);
        Canvas.SetTop(tb,  y);
        ChartCanvas.Children.Add(tb);
    }

    // ── Обработчики ──────────────────────────────────────────────────────

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var sysText   = SysBox.Text.Trim();
        var diaText   = DiaBox.Text.Trim();
        var pulseText = PulseBox.Text.Trim();
        var sugarText = SugarBox.Text.Trim();

        if (!int.TryParse(sysText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sys))
        { Warn("pressure_invalid_sys"); SysBox.Focus(); return; }
        if (!int.TryParse(diaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dia))
        { Warn("pressure_invalid_dia"); DiaBox.Focus(); return; }

        int? pulse = null;
        if (pulseText.Length > 0)
        {
            if (!int.TryParse(pulseText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p))
            { Warn("pressure_invalid_pulse"); PulseBox.Focus(); return; }
            pulse = p;
        }

        double? sugar = null;
        if (sugarText.Length > 0)
        {
            if (!double.TryParse(sugarText.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var sg)
                || sg < SugarMin || sg > SugarMax)
            { Warn("pressure_invalid_sugar"); SugarBox.Focus(); return; }
            sugar = sg;
        }

        // Проверка диапазонов
        bool outOfRange =
            sys < SysMin || sys > SysMax ||
            dia < DiaMin || dia > DiaMax ||
            (pulse.HasValue && (pulse < PulseMin || pulse > PulseMax));
        if (outOfRange)
        {
            if (MessageBox.Show(this, Loc.T("pressure_out_of_range"), Loc.T("warning_title"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        }

        // Предупреждение о нестандартном сахаре
        if (sugar.HasValue && (sugar < 3.0 || sugar > 20.0))
        {
            if (MessageBox.Show(this, Loc.T("pressure_sugar_unusual"), Loc.T("warning_title"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        }

        _ctx.State.PressureLog.Add(new PressureEntry
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            Systolic  = sys, Diastolic = dia, Pulse = pulse, Sugar = sugar
        });
        _ctx.SaveState();

        SysBox.Clear(); DiaBox.Clear(); PulseBox.Clear(); SugarBox.Clear();
        HeaderLabel.Text = Loc.T("pressure_header",
                                  ("date", DateTime.Now.ToString("yyyy-MM-dd HH:mm")));
        RenderHistory();
        DrawChart();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry == null)
        {
            MessageBox.Show(this, Loc.T("pressure_no_selection"),
                Loc.T("warning_title"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this,
                Loc.T("pressure_delete_confirm", ("ts", _selectedEntry.Timestamp)),
                Loc.T("confirm_title"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        _ctx.State.PressureLog.Remove(_selectedEntry);
        _ctx.SaveState();
        RenderHistory();
        DrawChart();
    }

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainTabs.SelectedItem == TabChart)
            DrawChart();
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Warn(string key) =>
        MessageBox.Show(this, Loc.T(key), Loc.T("warning_title"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
}
