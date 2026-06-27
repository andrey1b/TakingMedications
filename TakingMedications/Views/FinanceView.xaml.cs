using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using TakingMedications.Common;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class FinanceView : UserControl
{
    // Column widths — used only in ColumnDefinition, NOT on cell Borders
    private const double NoW       = 36.0;
    private const double MedW      = 210.0;
    private const double PriceW    = 80.0;
    private const double CountW    = 52.0;
    private const double TotPriceW = 84.0;
    private const double TotCountW = 52.0;
    private const double RowH      = 26.0;

    // Colors matching Python original
    private static readonly SolidColorBrush CHdrBg     = Hex("#37474F");
    private static readonly SolidColorBrush CSubPrice  = Hex("#455A64");
    private static readonly SolidColorBrush CSubCount  = Hex("#546E7A");
    private static readonly SolidColorBrush CTotColBg  = Hex("#1B5E20");
    private static readonly SolidColorBrush CTotColBg2 = Hex("#388E3C");
    private static readonly SolidColorBrush CRowTotBg  = Hex("#E8F5E9");
    private static readonly SolidColorBrush CRowTotFg  = Hex("#1B5E20");
    private static readonly SolidColorBrush CColTotBg  = Hex("#ECEFF1");
    private static readonly SolidColorBrush CColTotFg  = Hex("#0D47A1");
    private static readonly SolidColorBrush CGrandBg   = Hex("#1B5E20");
    private static readonly SolidColorBrush CNoRowBg   = Hex("#ECEFF1");
    private static readonly SolidColorBrush CBorder    = Hex("#CFD8DC");
    private static readonly SolidColorBrush CCellBg    = Brushes.White;
    private static readonly SolidColorBrush CCellBg2   = Hex("#FAFAFA");
    private static readonly SolidColorBrush CCellFg    = Brushes.Black;
    private static readonly SolidColorBrush CWhite     = Brushes.White;

    private MedAppContext? _ctx;
    private DispatcherTimer? _debounce;

    // Finance data
    private List<string> _dates = new();
    private Dictionary<string, Dictionary<string, string>> _cells  = new();
    private Dictionary<string, Dictionary<string, string>> _counts = new();

    // Meds: (id, name, subtitle) — deduplicated by name
    private List<(string id, string name, string subtitle)> _meds = new();

    // References to live UI elements for fast in-place updates
    private readonly Dictionary<(string mid, string date), TextBox> _priceBoxes = new();
    private readonly Dictionary<(string mid, string date), TextBox> _countBoxes = new();
    private readonly Dictionary<string, TextBlock> _rowTotPrice = new();
    private readonly Dictionary<string, TextBlock> _rowTotCount = new();
    private readonly Dictionary<string, TextBlock> _colTotPrice = new();
    private readonly Dictionary<string, TextBlock> _colTotCount = new();
    private TextBlock? _grandPriceLbl;
    private TextBlock? _grandCountLbl;

    public FinanceView()
    {
        InitializeComponent();
    }

    public void Initialize(MedAppContext ctx)
    {
        _ctx = ctx;
        LoadFinanceData();
        DeduplicateData();

        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _debounce.Tick += (_, _) => { _debounce.Stop(); RecalcNow(); };

        _ctx.DataChanged    += OnDataChanged;
        Loc.LanguageChanged += OnLangChanged;
        Unloaded += (_, _) =>
        {
            _ctx.DataChanged    -= OnDataChanged;
            Loc.LanguageChanged -= OnLangChanged;
        };

        BuildGrid();
    }

    // ────────────────────────────────────────────────────────────────
    //  Event handlers
    // ────────────────────────────────────────────────────────────────

    private void OnDataChanged()
    {
        _debounce?.Stop();
        RecalcNow(persist: false);
        LoadMeds();
        DeduplicateData();
        BuildGrid();
    }

    private void OnLangChanged() => BuildGrid();

    private void RightScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange   != 0) LeftScroll.ScrollToVerticalOffset(RightScroll.VerticalOffset);
        if (e.HorizontalChange != 0) RightHeaderScroll.ScrollToHorizontalOffset(RightScroll.HorizontalOffset);
        SyncLeftSpacer();
    }

    private void RightScroll_SizeChanged(object sender, SizeChangedEventArgs e) => SyncLeftSpacer();

    private void SyncLeftSpacer()
    {
        LeftScrollSpacer.Height = RightScroll.ComputedHorizontalScrollBarVisibility == Visibility.Visible
            ? SystemParameters.HorizontalScrollBarHeight : 0;
    }

    private void BtnAddDate_Click(object sender, RoutedEventArgs e) => ShowAddDateDialog();
    private void BtnDelDate_Click(object sender, RoutedEventArgs e) => DeleteLastDate();

    // ────────────────────────────────────────────────────────────────
    //  Data load / save
    // ────────────────────────────────────────────────────────────────

    private void LoadFinanceData()
    {
        _dates = new(); _cells = new(); _counts = new();
        if (!_ctx!.State.RawExtras.TryGetValue("_finance", out var token)
            || token is not JObject obj) return;

        if (obj["dates"] is JArray da)
            _dates = da.Select(t => t.ToString()).ToList();

        ReadDictInto(obj["cells"]  as JObject, _cells);
        ReadDictInto(obj["counts"] as JObject, _counts);
    }

    private static void ReadDictInto(JObject? src, Dictionary<string, Dictionary<string, string>> dst)
    {
        if (src == null) return;
        foreach (var prop in src.Properties())
        {
            if (prop.Value is not JObject inner) continue;
            var d = new Dictionary<string, string>();
            foreach (var p2 in inner.Properties())
                if (!string.IsNullOrEmpty(p2.Value.ToString()))
                    d[p2.Name] = p2.Value.ToString();
            if (d.Count > 0) dst[prop.Name] = d;
        }
    }

    private void SaveFinanceData()
    {
        if (_ctx == null) return;
        var fin = new JObject();
        fin["dates"]  = new JArray(_dates.Cast<object>().ToArray());
        fin["cells"]  = ToJObj(_cells);
        fin["counts"] = ToJObj(_counts);
        _ctx.State.RawExtras["_finance"] = fin;
        _ctx.SaveState();
    }

    private static JObject ToJObj(Dictionary<string, Dictionary<string, string>> src)
    {
        var j = new JObject();
        foreach (var (mid, dd) in src)
        {
            var inner = new JObject();
            foreach (var (date, val) in dd)
                if (!string.IsNullOrEmpty(val)) inner[date] = val;
            if (inner.Count > 0) j[mid] = inner;
        }
        return j;
    }

    // ────────────────────────────────────────────────────────────────
    //  Meds / dedup
    // ────────────────────────────────────────────────────────────────

    private void LoadMeds()
    {
        _meds = new();
        var seen = new HashSet<string>();
        foreach (var sec in _ctx!.Sections)
            foreach (var item in sec.Items)
            {
                var nk = (item.Name ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(nk) || !seen.Add(nk)) continue;
                _meds.Add((item.Id, item.Name ?? "", item.Subtitle ?? ""));
            }
    }

    private void DeduplicateData()
    {
        var nameToMids = new Dictionary<string, List<string>>();
        foreach (var sec in _ctx!.Sections)
            foreach (var item in sec.Items)
            {
                var nk = (item.Name ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(nk)) continue;
                if (!nameToMids.TryGetValue(nk, out var list))
                    nameToMids[nk] = list = new();
                list.Add(item.Id);
            }

        foreach (var (_, mids) in nameToMids)
        {
            if (mids.Count <= 1) continue;
            var primary = mids[0];
            foreach (var dup in mids.Skip(1))
                MergeMid(dup, primary);
        }
    }

    private void MergeMid(string dup, string primary)
    {
        foreach (var store in new[] { _cells, _counts })
        {
            if (!store.TryGetValue(dup, out var dupRow)) continue;
            if (!store.ContainsKey(primary)) store[primary] = new();
            foreach (var (date, val) in dupRow)
            {
                double cur = ParseDouble(store[primary].GetValueOrDefault(date, ""));
                double add = ParseDouble(val);
                if (add == 0) continue;
                double total = cur + add;
                store[primary][date] = total == Math.Floor(total)
                    ? ((int)total).ToString()
                    : total.ToString("0.00", CultureInfo.InvariantCulture);
            }
            store.Remove(dup);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Grid build
    // ────────────────────────────────────────────────────────────────

    private void BuildGrid()
    {
        if (_ctx == null) return;

        LoadMeds();

        _priceBoxes.Clear(); _countBoxes.Clear();
        _rowTotPrice.Clear(); _rowTotCount.Clear();
        _colTotPrice.Clear(); _colTotCount.Clear();
        _grandPriceLbl = null; _grandCountLbl = null;

        static void ClearGrid(Grid g)
        {
            g.Children.Clear();
            g.RowDefinitions.Clear();
            g.ColumnDefinitions.Clear();
        }

        ClearGrid(LeftHeaderGrid);
        ClearGrid(RightHeaderGrid);
        ClearGrid(LeftGrid);
        ClearGrid(RightGrid);

        BtnAddDate.Content = Loc.T("btn_add_date");
        BtnDelDate.Content = Loc.T("btn_del_last");

        if (_meds.Count == 0)
        {
            GrandTotalLabel.Text = Loc.T("fin_no_meds");
            return;
        }

        // ── Row definitions ─────────────────────────────────────────
        // Header grids: 2 rows each
        for (int r = 0; r < 2; r++)
        {
            LeftHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowH) });
            RightHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowH) });
        }
        // Data grids: N med rows + 1 col-total row
        for (int r = 0; r < _meds.Count + 1; r++)
        {
            LeftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowH) });
            RightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowH) });
        }

        // ── Column definitions (same in header and data grids) ──────
        foreach (var g in new[] { LeftHeaderGrid, LeftGrid })
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NoW) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(MedW) });
        }
        foreach (var g in new[] { RightHeaderGrid, RightGrid })
        {
            foreach (var _ in _dates)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PriceW) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CountW) });
            }
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TotPriceW) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TotCountW) });
        }

        int totPriceCol = _dates.Count * 2;
        int totCountCol = totPriceCol + 1;

        // ── LEFT HEADER (rows 0-1 of LeftHeaderGrid) ────────────────
        Put(LeftHeaderGrid, Hdr(Loc.T("fin_col_no"),  CHdrBg, CWhite, fw: FontWeights.Bold, align: TextAlignment.Center), 0, 0);
        Put(LeftHeaderGrid, Hdr(Loc.T("fin_col_med"), CHdrBg, CWhite, fw: FontWeights.Bold, align: TextAlignment.Left, pad: new Thickness(6, 0, 4, 0)), 0, 1);
        Put(LeftHeaderGrid, Hdr("", CHdrBg, CWhite), 1, 0);
        Put(LeftHeaderGrid, Hdr("", CHdrBg, CWhite), 1, 1);

        // ── RIGHT HEADER row 0: date labels ─────────────────────────
        for (int ci = 0; ci < _dates.Count; ci++)
        {
            var h = Hdr(_dates[ci], CHdrBg, CWhite, fw: FontWeights.Bold, align: TextAlignment.Center);
            Grid.SetColumnSpan(h, 2);
            Put(RightHeaderGrid, h, 0, ci * 2);
        }
        var totHdr = Hdr(Loc.T("fin_col_total"), CTotColBg, CWhite, fw: FontWeights.Bold, align: TextAlignment.Center);
        Grid.SetColumnSpan(totHdr, 2);
        Put(RightHeaderGrid, totHdr, 0, totPriceCol);

        // ── RIGHT HEADER row 1: ₴ / шт sub-headers ──────────────────
        for (int ci = 0; ci < _dates.Count; ci++)
        {
            Put(RightHeaderGrid, Hdr(Loc.T("fin_currency"), CSubPrice, CWhite, fs: 9, fw: FontWeights.Bold, align: TextAlignment.Center), 1, ci * 2);
            Put(RightHeaderGrid, Hdr(Loc.T("fin_units"),    CSubCount, CWhite, fs: 9, fw: FontWeights.Bold, align: TextAlignment.Center), 1, ci * 2 + 1);
        }
        Put(RightHeaderGrid, Hdr(Loc.T("fin_currency"), CTotColBg,  CWhite, fs: 9, fw: FontWeights.Bold, align: TextAlignment.Center), 1, totPriceCol);
        Put(RightHeaderGrid, Hdr(Loc.T("fin_units"),    CTotColBg2, CWhite, fs: 9, fw: FontWeights.Bold, align: TextAlignment.Center), 1, totCountCol);

        // ── DATA ROWS (rows 0..N-1 of LeftGrid / RightGrid) ─────────
        for (int ri = 0; ri < _meds.Count; ri++)
        {
            var (mid, name, subtitle) = _meds[ri];
            int row = ri;   // data grids start at row 0

            Put(LeftGrid, Hdr((ri + 1).ToString(), CNoRowBg, CCellFg, align: TextAlignment.Center), row, 0);
            var nameText = string.IsNullOrEmpty(subtitle) ? name : $"{name} ({subtitle})";
            Put(LeftGrid, Hdr(nameText, CCellBg, CCellFg, align: TextAlignment.Left, pad: new Thickness(6, 0, 4, 0)), row, 1);

            for (int ci = 0; ci < _dates.Count; ci++)
            {
                var date = _dates[ci];
                var pRaw = _cells.TryGetValue(mid, out var cm)  ? cm.GetValueOrDefault(date, "")  : "";
                var cRaw = _counts.TryGetValue(mid, out var cm2) ? cm2.GetValueOrDefault(date, "") : "";

                var pBox = MakePriceBox(FmtMoney(pRaw));
                _priceBoxes[(mid, date)] = pBox;
                Put(RightGrid, Wrap(pBox, CCellBg), row, ci * 2);

                var cBox = MakeCountBox(cRaw);
                _countBoxes[(mid, date)] = cBox;
                Put(RightGrid, Wrap(cBox, CCellBg2), row, ci * 2 + 1);
            }

            var rtpLbl = Lbl("", CRowTotFg, fw: FontWeights.SemiBold, pad: new Thickness(0, 0, 6, 0));
            _rowTotPrice[mid] = rtpLbl;
            Put(RightGrid, Wrap(rtpLbl, CRowTotBg), row, totPriceCol);

            var rtcLbl = Lbl("", CRowTotFg, fw: FontWeights.SemiBold, pad: new Thickness(0, 0, 4, 0));
            _rowTotCount[mid] = rtcLbl;
            Put(RightGrid, Wrap(rtcLbl, CRowTotBg), row, totCountCol);
        }

        // ── COLUMN TOTALS ROW (row N in data grids) ──────────────────
        int ctRow = _meds.Count;
        Put(LeftGrid, Hdr("", CColTotBg, CCellFg), ctRow, 0);
        Put(LeftGrid, Hdr(Loc.T("fin_total_by_date"), CColTotBg, CColTotFg, fw: FontWeights.Bold,
                          align: TextAlignment.Left, pad: new Thickness(6, 0, 4, 0)), ctRow, 1);

        for (int ci = 0; ci < _dates.Count; ci++)
        {
            var date = _dates[ci];
            var ctp = Lbl("", CColTotFg, fw: FontWeights.Bold, pad: new Thickness(0, 0, 6, 0));
            _colTotPrice[date] = ctp;
            Put(RightGrid, Wrap(ctp, CColTotBg), ctRow, ci * 2);
            var ctc = Lbl("", CColTotFg, fw: FontWeights.Bold, pad: new Thickness(0, 0, 4, 0));
            _colTotCount[date] = ctc;
            Put(RightGrid, Wrap(ctc, CColTotBg), ctRow, ci * 2 + 1);
        }

        _grandPriceLbl = Lbl("0,00", CWhite, fw: FontWeights.Bold, pad: new Thickness(0, 0, 6, 0));
        Put(RightGrid, Wrap(_grandPriceLbl, CGrandBg), ctRow, totPriceCol);
        _grandCountLbl = Lbl("", CWhite, fw: FontWeights.Bold, pad: new Thickness(0, 0, 4, 0));
        Put(RightGrid, Wrap(_grandCountLbl, CGrandBg), ctRow, totCountCol);

        RecalcNow(persist: false);

        // Синхронизировать спейсер после финальной компоновки
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(SyncLeftSpacer));
    }

    // ────────────────────────────────────────────────────────────────
    //  Recalculation
    // ────────────────────────────────────────────────────────────────

    private void ScheduleRecalc()
    {
        _debounce?.Stop();
        _debounce?.Start();
    }

    private void RecalcNow(bool persist = true)
    {
        if (_ctx == null) return;

        double grand = 0;
        int    grandCnt = 0;

        foreach (var (mid, _, _) in _meds)
        {
            if (!_cells.ContainsKey(mid))  _cells[mid]  = new();
            if (!_counts.ContainsKey(mid)) _counts[mid] = new();

            double rowSum = 0;
            int    rowCnt = 0;

            foreach (var date in _dates)
            {
                if (_priceBoxes.TryGetValue((mid, date), out var pb))
                {
                    double v = ParseDouble(pb.Text);
                    _cells[mid][date] = v != 0 ? pb.Text.Trim() : "";
                    rowSum += v;
                }
                if (_countBoxes.TryGetValue((mid, date), out var cb))
                {
                    int cv = ParseInt(cb.Text);
                    _counts[mid][date] = cv != 0 ? cv.ToString() : "";
                    rowCnt += cv;
                }
            }

            if (_rowTotPrice.TryGetValue(mid, out var rtpl)) rtpl.Text = FmtMoney(rowSum, false);
            if (_rowTotCount.TryGetValue(mid, out var rtcl)) rtcl.Text = rowCnt > 0 ? rowCnt.ToString() : "";

            grand    += rowSum;
            grandCnt += rowCnt;
        }

        foreach (var date in _dates)
        {
            double cs = 0; int cc = 0;
            foreach (var (mid, _, _) in _meds)
            {
                if (_cells.TryGetValue(mid, out var dm)  && dm.TryGetValue(date, out var v))  cs += ParseDouble(v);
                if (_counts.TryGetValue(mid, out var dc) && dc.TryGetValue(date, out var cv)) cc += ParseInt(cv);
            }
            if (_colTotPrice.TryGetValue(date, out var ctp)) ctp.Text = FmtMoney(cs, false);
            if (_colTotCount.TryGetValue(date, out var ctc)) ctc.Text = cc > 0 ? cc.ToString() : "";
        }

        if (_grandPriceLbl != null) _grandPriceLbl.Text = FmtMoney(grand, true);
        if (_grandCountLbl != null) _grandCountLbl.Text = grandCnt > 0 ? grandCnt.ToString() : "";

        var cntPart = grandCnt > 0 ? Loc.T("fin_grand_label_cnt", ("cnt", grandCnt.ToString())) : "";
        GrandTotalLabel.Text = Loc.T("fin_grand_label",
            ("amount",   FmtMoney(grand, true)),
            ("currency", Loc.T("fin_currency"))) + cntPart;

        if (persist) SaveFinanceData();
    }

    // ────────────────────────────────────────────────────────────────
    //  Add / delete date dialogs
    // ────────────────────────────────────────────────────────────────

    private void ShowAddDateDialog()
    {
        var dlg = new Window
        {
            Title = Loc.T("dlg_add_date_title"),
            Width = 360, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("BgCardBrush"),
        };

        var stack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        stack.Children.Add(new TextBlock
        {
            Text = Loc.T("dlg_add_date_label"),
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            FontSize = 12, Margin = new Thickness(0, 0, 0, 6)
        });
        var tb = new TextBox
        {
            Text = DateTime.Now.ToString("dd.MM.yyyy"),
            FontSize = 13, Padding = new Thickness(6, 5, 6, 5),
            Margin = new Thickness(0, 0, 0, 4)
        };
        stack.Children.Add(tb);
        var errLbl = new TextBlock
        {
            Text = "", Foreground = Brushes.OrangeRed, FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            Visibility = Visibility.Collapsed
        };
        stack.Children.Add(errLbl);

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var okBtn = new Button
        {
            Content = Loc.T("btn_save"),
            Style = (Style)FindResource("RoundButton"),
            Background = (Brush)FindResource("SuccessBrush"),
            Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(16, 5, 16, 5)
        };
        var cancelBtn = new Button
        {
            Content = Loc.T("btn_cancel"),
            Style = (Style)FindResource("RoundButton"),
            Padding = new Thickness(16, 5, 16, 5)
        };
        btnRow.Children.Add(okBtn);
        btnRow.Children.Add(cancelBtn);
        stack.Children.Add(btnRow);
        dlg.Content = stack;

        void TryConfirm()
        {
            var val = tb.Text.Trim();
            if (string.IsNullOrEmpty(val))
            { errLbl.Text = Loc.T("dlg_add_date_empty"); errLbl.Visibility = Visibility.Visible; return; }
            if (!IsValidDate(val))
            { errLbl.Text = Loc.T("dlg_add_date_invalid"); errLbl.Visibility = Visibility.Visible; return; }
            if (_dates.Contains(val))
            { errLbl.Text = Loc.T("dlg_add_date_dup", ("val", val)); errLbl.Visibility = Visibility.Visible; return; }
            dlg.DialogResult = true;
        }

        okBtn.Click     += (_, _) => TryConfirm();
        cancelBtn.Click += (_, _) => dlg.Close();
        tb.KeyDown      += (_, e) => { if (e.Key == Key.Return) TryConfirm(); };

        if (dlg.ShowDialog() != true) return;

        _dates.Add(tb.Text.Trim());
        _dates.Sort(DateSortKey);
        SaveFinanceData();
        BuildGrid();
    }

    private void DeleteLastDate()
    {
        if (_dates.Count == 0)
        {
            MessageBox.Show(Window.GetWindow(this), Loc.T("fin_no_dates_msg"),
                Loc.T("fin_no_data"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _debounce?.Stop();
        RecalcNow(persist: false);

        var last = _dates[^1];
        int filled = _meds.Count(m =>
        {
            bool hp = _cells.TryGetValue(m.id, out var d)   && d.TryGetValue(last, out var v)   && !string.IsNullOrEmpty(v);
            bool hc = _counts.TryGetValue(m.id, out var d2) && d2.TryGetValue(last, out var v2) && !string.IsNullOrEmpty(v2);
            return hp || hc;
        });

        var detail = filled > 0
            ? Loc.T("fin_del_col_filled", ("n", filled.ToString()))
            : Loc.T("fin_del_col_empty");

        var ans = MessageBox.Show(Window.GetWindow(this),
            Loc.T("fin_del_col_msg", ("date", last)) + "\n\n" + detail,
            Loc.T("fin_del_col_title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ans != MessageBoxResult.Yes) return;

        _dates.RemoveAt(_dates.Count - 1);
        foreach (var (mid, _, _) in _meds)
        {
            _cells.TryGetValue(mid,  out var dc);  dc?.Remove(last);
            _counts.TryGetValue(mid, out var dcc); dcc?.Remove(last);
        }
        SaveFinanceData();
        BuildGrid();
    }

    // ────────────────────────────────────────────────────────────────
    //  UI element factories — all stretch to fill their Grid cell
    // ────────────────────────────────────────────────────────────────

    private TextBox MakePriceBox(string value)
    {
        var tb = new TextBox
        {
            Text = value, Background = CCellBg, Foreground = CCellFg,
            FontSize = 12, TextAlignment = TextAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(4, 2, 4, 2), BorderThickness = new Thickness(0),
        };
        tb.LostFocus += (_, _) => { tb.Text = FmtMoney(tb.Text); ScheduleRecalc(); };
        tb.KeyDown   += (_, e) => { if (e.Key == Key.Return) ScheduleRecalc(); };
        return tb;
    }

    private TextBox MakeCountBox(string value)
    {
        var tb = new TextBox
        {
            Text = value, Background = CCellBg2, Foreground = CCellFg,
            FontSize = 12, TextAlignment = TextAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(4, 2, 4, 2), BorderThickness = new Thickness(0),
        };
        tb.LostFocus += (_, _) =>
        {
            int v = ParseInt(tb.Text);
            tb.Text = v > 0 ? v.ToString() : "";
            ScheduleRecalc();
        };
        tb.KeyDown += (_, e) => { if (e.Key == Key.Return) ScheduleRecalc(); };
        return tb;
    }

    // Header cell — Border + TextBlock, stretches to fill grid cell
    private static Border Hdr(string text, SolidColorBrush bg, SolidColorBrush fg,
        double fs = 10, FontWeight? fw = null,
        TextAlignment align = TextAlignment.Center,
        Thickness? pad = null)
        => new Border
        {
            Background      = bg,
            BorderBrush     = CBorder,
            BorderThickness = new Thickness(0, 0, 1, 1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch,
            Child = new TextBlock
            {
                Text = text, Foreground = fg, FontSize = fs,
                FontWeight = fw ?? FontWeights.Normal,
                TextAlignment = align,
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = pad ?? new Thickness(4, 0, 4, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            }
        };

    // Label wrapped in a border — for total cells
    private static TextBlock Lbl(string text, SolidColorBrush fg,
        FontWeight? fw = null, Thickness? pad = null)
        => new TextBlock
        {
            Text = text, Foreground = fg, FontSize = 12,
            FontWeight = fw ?? FontWeights.Normal,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = pad ?? new Thickness(0, 0, 6, 0),
        };

    // Wraps any UIElement in a border that stretches to fill the grid cell
    private static Border Wrap(UIElement child, SolidColorBrush bg)
        => new Border
        {
            Background      = bg,
            BorderBrush     = CBorder,
            BorderThickness = new Thickness(0, 0, 1, 1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch,
            Child = child,
        };

    private static void Put(Grid grid, UIElement el, int row, int col)
    {
        Grid.SetRow(el, row);
        Grid.SetColumn(el, col);
        grid.Children.Add(el);
    }

    // ────────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────────

    private static SolidColorBrush Hex(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));

    private static string FmtMoney(double v, bool allowZero = true)
    {
        if (v == 0 && !allowZero) return "";
        return v.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    private static string FmtMoney(string raw, bool allowZero = false)
        => FmtMoney(ParseDouble(raw), allowZero);

    private static double ParseDouble(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Any,
                CultureInfo.InvariantCulture, out double v))
            return v < 0 ? 0 : v;
        return 0;
    }

    private static int ParseInt(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var clean = s.Replace(',', '.').Split('.')[0];
        if (int.TryParse(clean, out int v)) return v < 0 ? 0 : v;
        return 0;
    }

    private static bool IsValidDate(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        var p = s.Split('.');
        if (p.Length != 3) return false;
        if (!int.TryParse(p[0], out int d) || !int.TryParse(p[1], out int m) || !int.TryParse(p[2], out int y))
            return false;
        try { _ = new DateTime(y, m, d); return true; } catch { return false; }
    }

    private static int DateSortKey(string a, string b)
    {
        static int Key(string s)
        {
            var p = s.Split('.');
            return p.Length == 3
                   && int.TryParse(p[0], out int d)
                   && int.TryParse(p[1], out int mo)
                   && int.TryParse(p[2], out int y)
                ? y * 10000 + mo * 100 + d : 0;
        }
        return Key(a).CompareTo(Key(b));
    }
}
