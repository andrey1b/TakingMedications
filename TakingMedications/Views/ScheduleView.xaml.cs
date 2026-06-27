using System;
using System.Collections.Generic;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using TakingMedications.Common;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class ScheduleView : UserControl
{
    private MedAppContext? _ctx;
    private DateTime _currentDate = DateTime.Today;
    private readonly Dictionary<string, bool> _collapsed = new();
    private readonly HashSet<string> _expandedCourses = new();

    public ScheduleView()
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
        LoadCollapseState();
        Render();
    }

    private void LoadCollapseState()
    {
        if (_ctx?.State.RawExtras.TryGetValue("_ui", out var uiToken) != true) return;
        if (uiToken is not JObject uiObj) return;
        if (uiObj["sections_collapsed"] is not JObject sc) return;
        foreach (var prop in sc.Properties())
            _collapsed[prop.Name] = prop.Value.Value<bool>();
    }

    private void PersistCollapseState(string key, bool collapsed)
    {
        if (_ctx == null) return;
        if (!_ctx.State.RawExtras.TryGetValue("_ui", out var uiToken) || uiToken is not JObject uiObj)
        {
            uiObj = new JObject();
            _ctx.State.RawExtras["_ui"] = uiObj;
        }
        if (uiObj["sections_collapsed"] is not JObject sc)
        {
            sc = new JObject();
            uiObj["sections_collapsed"] = sc;
        }
        sc[key] = collapsed;
        _ctx.SaveState();
    }

    public void Render()
    {
        if (_ctx == null) return;

        BtnToday.Content  = Loc.T("btn_today");
        BtnNow.Content    = Loc.T("btn_now");
        BtnManage.Content = Loc.T("btn_manage");
        DateLabel.Text    = Loc.FormatDateLong(_currentDate);
        DateSubLabel.Text = Loc.WeekdayFull(IsoDayOfWeek(_currentDate));

        SectionsPanel.Children.Clear();
        var iso = _currentDate.ToString("yyyy-MM-dd");

        int total = 0, taken = 0;
        foreach (var section in _ctx.Sections)
        {
            if (section.Items.Count == 0) continue;
            var card = BuildSectionCard(section, iso, out var s, out var t);
            total += s; taken += t;
            SectionsPanel.Children.Add(card);
        }

        StatsLabel.Text = total == 0
            ? Loc.T("schedule_empty")
            : Loc.T("schedule_stats", ("taken", taken), ("total", total));
    }

    // ────────────────────────────────────────────────────────────────
    //  Section card
    // ────────────────────────────────────────────────────────────────

    private Border BuildSectionCard(MedicationSection section, string iso,
                                    out int slotsTotal, out int slotsTaken)
    {
        slotsTotal = section.Items.Count;
        slotsTaken = section.Items.Count(m => _ctx!.State.IsTaken(iso, m.Id));

        bool collapsed = _collapsed.GetValueOrDefault(section.SectionKey, false);
        var accent = SectionAccent(section.SectionKey);

        // ── Indicator triangle ───────────────────────────────────────
        var indicator = new TextBlock
        {
            Text              = collapsed ? "▶" : "▼",
            FontSize          = 11,
            Foreground        = accent,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0),
        };

        // ── Title ────────────────────────────────────────────────────
        var titleTb = new TextBlock
        {
            Text              = section.Title,
            FontSize          = 16,
            FontWeight        = FontWeights.Bold,
            Foreground        = accent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // ── Count badge — виден только при свёрнутой секции ──────────
        var countBadge = new TextBlock
        {
            Text              = $"  ({slotsTaken}/{slotsTotal})",
            FontSize          = 13,
            Foreground        = accent,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility        = collapsed ? Visibility.Visible : Visibility.Collapsed,
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Cursor      = Cursors.Hand,
            Margin      = new Thickness(0, 0, 0, collapsed ? 0 : 8),
        };
        header.Children.Add(indicator);
        header.Children.Add(titleTb);
        header.Children.Add(countBadge);

        // ── Content panel ────────────────────────────────────────────
        var content = new StackPanel
        {
            Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible,
        };
        var items = section.Items;
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0)
                content.Children.Add(new Rectangle
                {
                    Height = 1,
                    Fill   = (Brush)FindResource("SeparatorBrush"),
                    Margin = new Thickness(0, 2, 0, 2),
                });
            content.Children.Add(BuildMedRow(items[i], iso));
        }

        // ── Toggle on header click ───────────────────────────────────
        header.MouseLeftButtonUp += (_, _) =>
        {
            bool nowCollapsed = !_collapsed.GetValueOrDefault(section.SectionKey, false);
            _collapsed[section.SectionKey] = nowCollapsed;
            indicator.Text        = nowCollapsed ? "▶" : "▼";
            content.Visibility    = nowCollapsed ? Visibility.Collapsed : Visibility.Visible;
            countBadge.Visibility = nowCollapsed ? Visibility.Visible   : Visibility.Collapsed;
            header.Margin         = new Thickness(0, 0, 0, nowCollapsed ? 0 : 8);
            PersistCollapseState(section.SectionKey, nowCollapsed);
        };

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(content);

        return new Border
        {
            Background      = (Brush)FindResource("BgCardBrush"),
            BorderBrush     = (Brush)FindResource("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(12),
            Padding         = new Thickness(14, 12, 14, 12),
            Margin          = new Thickness(0, 0, 0, 10),
            Child           = stack,
        };
    }

    // ────────────────────────────────────────────────────────────────
    //  Medication row
    // ────────────────────────────────────────────────────────────────

    private FrameworkElement BuildMedRow(Medication med, string iso)
    {
        var course     = GetCourse(med);
        bool isFinished = course?.Finished == true;
        bool isExpanded = _expandedCourses.Contains(med.Id);

        // ── Compact row (только для завершённого курса) ───────────────
        Border? compactBorder = null;
        if (isFinished)
        {
            var grey = new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0x9C));
            var compRow = new WrapPanel { Orientation = Orientation.Horizontal };
            compRow.Children.Add(new TextBlock { Text = "▶  ", FontSize = 10, Foreground = grey, VerticalAlignment = VerticalAlignment.Center });
            compRow.Children.Add(new TextBlock { Text = med.Name, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = grey, VerticalAlignment = VerticalAlignment.Center });
            compRow.Children.Add(new TextBlock
            {
                Text = "  —  " + Loc.T("course_finished", ("start", course!.Start.ToString("dd.MM.yyyy"))),
                FontSize = 11, Foreground = grey, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
            });
            var showBtn = MakeClickBadge(Loc.T("course_show_btn"), "#455A64");
            compRow.Children.Add(showBtn);

            compactBorder = new Border
            {
                Padding    = new Thickness(0, 5, 0, 5),
                Visibility = isExpanded ? Visibility.Collapsed : Visibility.Visible,
                Child      = compRow,
            };
            showBtn.MouseLeftButtonUp += (_, _) => ToggleCourseExpand(med.Id, compactBorder, null);
        }

        // ── Full row ──────────────────────────────────────────────────
        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var timeTb = new TextBlock
        {
            Text              = med.Time,
            FontSize          = 14,
            FontWeight        = FontWeights.Bold,
            Foreground        = (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(timeTb, 0);
        mainGrid.Children.Add(timeTb);

        var cb = new CheckBox
        {
            Style             = (Style)FindResource("DarkCheckBox"),
            IsChecked         = _ctx!.State.IsTaken(iso, med.Id),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(4, 0, 12, 0),
        };
        bool suppressCheck = false;
        cb.Checked += (_, _) =>
        {
            if (suppressCheck) return;
            int stock = GetStock(med);
            if (stock <= 0)
            {
                suppressCheck = true;
                cb.IsChecked = false;
                suppressCheck = false;
                MessageBox.Show(
                    Window.GetWindow(this),
                    Loc.T("taken_no_stock_msg"),
                    Loc.T("taken_no_stock_title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            MarkTaken(med, iso, true);
        };
        cb.Unchecked += (_, _) =>
        {
            if (suppressCheck) return;
            MarkTaken(med, iso, false);
        };
        Grid.SetColumn(cb, 1);
        mainGrid.Children.Add(cb);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        // ── Name row ─────────────────────────────────────────────────
        var nameGrid = new Grid();
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameText = !string.IsNullOrEmpty(med.Subtitle)
            ? $"{med.Name}  ·  {med.Subtitle}"
            : med.Name;
        var nameTb = new TextBlock
        {
            Text              = nameText,
            FontSize          = 14,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = (Brush)FindResource("TextPrimaryBrush"),
            Cursor            = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
        };
        nameTb.MouseEnter        += (_, _) => nameTb.TextDecorations = TextDecorations.Underline;
        nameTb.MouseLeave        += (_, _) => nameTb.TextDecorations = null;
        nameTb.MouseLeftButtonUp += (_, _) => OpenSearch(med.Name);
        Grid.SetColumn(nameTb, 0);
        nameGrid.Children.Add(nameTb);

        var rightPanel = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(6, 0, 0, 0),
        };

        if (!isFinished)
        {
            var courseBadge = MakeCourseBadge(med, course);
            if (courseBadge != null) rightPanel.Children.Add(courseBadge);
        }

        if (!string.IsNullOrEmpty(med.Doctor))
            rightPanel.Children.Add(MakeBadge(
                Loc.T("schedule_doctor", ("doctor", med.Doctor)), "#546E7A"));

        if (!string.IsNullOrEmpty(med.Pharmacy))
        {
            var pharmBtn = MakeClickBadge(Loc.T("btn_pharmacy"), "#1565C0");
            pharmBtn.MouseLeftButtonUp += (_, _) => OpenPharmacy(med);
            rightPanel.Children.Add(pharmBtn);
        }

        TextBlock? descPanel = null;
        if (!string.IsNullOrEmpty(med.Description))
        {
            descPanel = new TextBlock
            {
                Text         = med.Description,
                FontSize     = 12,
                Foreground   = (Brush)FindResource("TextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 3, 0, 0),
                Visibility   = Visibility.Collapsed,
            };

            bool descExpanded = false;
            var descBtn   = MakeClickBadge(Loc.T("btn_description"), "#37474F");
            var descBtnTb = (TextBlock)descBtn.Child;
            descBtn.MouseLeftButtonUp += (_, _) =>
            {
                descExpanded         = !descExpanded;
                descPanel.Visibility = descExpanded ? Visibility.Visible : Visibility.Collapsed;
                descBtnTb.Text       = descExpanded
                    ? Loc.T("btn_description").Replace("▸", "▾")
                    : Loc.T("btn_description");
            };
            rightPanel.Children.Add(descBtn);
        }

        Grid.SetColumn(rightPanel, 1);
        nameGrid.Children.Add(rightPanel);
        info.Children.Add(nameGrid);

        // ── Sub-text: TimeNote, Note ──────────────────────────────────
        var sub = new List<string>();
        if (!string.IsNullOrEmpty(med.TimeNote)) sub.Add(med.TimeNote);
        if (!string.IsNullOrEmpty(med.Note))     sub.Add(med.Note);
        if (sub.Count > 0)
        {
            info.Children.Add(new TextBlock
            {
                Text         = string.Join("  ·  ", sub),
                FontSize     = 12,
                Foreground   = (Brush)FindResource("TextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 1, 0, 0),
            });
        }

        // ── Course row ────────────────────────────────────────────────
        var courseRow = BuildCourseRow(med, course);
        if (courseRow != null) info.Children.Add(courseRow);

        // ── Stock row ─────────────────────────────────────────────────
        var stock     = GetStock(med);
        var purchased = GetPurchased(med);
        if (stock >= 0)
        {
            info.Children.Add(new TextBlock
            {
                Text       = stock > 0
                    ? Loc.T("stock_available", ("n", stock))
                    : Loc.T("stock_empty"),
                FontSize   = 11,
                Foreground = stock > 0
                    ? new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84))
                    : new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
                Margin     = new Thickness(0, 1, 0, 0),
            });
        }
        else if (purchased == false)
        {
            info.Children.Add(new TextBlock
            {
                Text       = Loc.T("stock_not_purchased"),
                FontSize   = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
                Margin     = new Thickness(0, 1, 0, 0),
            });
        }

        if (descPanel != null) info.Children.Add(descPanel);

        Grid.SetColumn(info, 2);
        mainGrid.Children.Add(info);

        var fullBorder = new Border
        {
            Padding    = new Thickness(0, 6, 0, 6),
            Child      = mainGrid,
            Visibility = (isFinished && !isExpanded) ? Visibility.Collapsed : Visibility.Visible,
        };

        if (!isFinished) return fullBorder;

        // Регистрируем пару compact↔full, чтобы кнопка "Скрыть" в развёрнутом блоке работала
        _finishedPairs[med.Id] = (compactBorder!, fullBorder);

        var container = new StackPanel();
        container.Children.Add(compactBorder!);
        container.Children.Add(fullBorder);
        return container;
    }

    // Держит пары compact↔full для каждого medId, чтобы кнопка "Скрыть" имела доступ
    private readonly Dictionary<string, (Border compact, Border full)> _finishedPairs = new();

    private void ToggleCourseExpand(string medId, Border? compact, Border? full)
    {
        // Если compact/full не переданы — берём из словаря
        if (compact == null || full == null)
        {
            if (!_finishedPairs.TryGetValue(medId, out var pair)) return;
            compact = pair.compact;
            full    = pair.full;
        }

        bool nowExpanded = !_expandedCourses.Contains(medId);
        if (nowExpanded) _expandedCourses.Add(medId);
        else             _expandedCourses.Remove(medId);

        compact.Visibility = nowExpanded ? Visibility.Collapsed : Visibility.Visible;
        full.Visibility    = nowExpanded ? Visibility.Visible   : Visibility.Collapsed;
    }

    // ────────────────────────────────────────────────────────────────
    //  Course row builder
    // ────────────────────────────────────────────────────────────────

    private FrameworkElement? BuildCourseRow(Medication med, CourseData? course)
    {
        bool showCourse = course != null || !string.IsNullOrEmpty(med.Course);
        if (!showCourse) return null;

        var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };

        // ── Активный / бессрочный курс ───────────────────────────────
        if (course != null && (course.Active || course.Total == 0))
        {
            bool indefinite = course.Total == 0;
            string progressText = indefinite
                ? Loc.T("course_label",
                      ("text", course.Start.ToString("dd.MM.yyyy") + ", " +
                               Loc.T("course_days_left", ("n", course.DayNum - 1))))
                : Loc.T("course_progress",
                      ("start", course.Start.ToString("dd.MM.yyyy")),
                      ("day",   course.DayNum),
                      ("total", course.Total),
                      ("pct",   course.Percent));

            panel.Children.Add(new TextBlock
            {
                Text              = progressText,
                FontSize          = 11,
                Foreground        = (Brush)FindResource("CourseBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 6, 0),
            });
            var extendBtn = MakeClickBadge(Loc.T("course_extend_btn"), "#1565C0");
            extendBtn.MouseLeftButtonUp += (_, _) => ShowExtendCourseDialog(med, course);
            panel.Children.Add(extendBtn);
            return panel;
        }

        // ── Завершённый курс — кнопка "Скрыть" + "Начать заново" ────
        if (course != null && course.Finished)
        {
            panel.Children.Add(new TextBlock
            {
                Text      = Loc.T("course_finished", ("start", course.Start.ToString("dd.MM.yyyy"))),
                FontSize  = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAE)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin    = new Thickness(0, 0, 6, 0),
            });
            var newBtn = MakeClickBadge(Loc.T("course_start_btn"), "#2E7D32");
            newBtn.MouseLeftButtonUp += (_, _) => ShowStartCourseDialog(med);
            panel.Children.Add(newBtn);
            var hideBtn = MakeClickBadge(Loc.T("course_hide_btn"), "#455A64");
            hideBtn.MouseLeftButtonUp += (_, _) => ToggleCourseExpand(med.Id, null, null);
            panel.Children.Add(hideBtn);
            return panel;
        }

        // ── Курс ещё не начат ────────────────────────────────────────
        var startBtn = MakeClickBadge(Loc.T("course_start_btn"), "#2E7D32");
        startBtn.MouseLeftButtonUp += (_, _) => ShowStartCourseDialog(med);
        panel.Children.Add(startBtn);
        return panel;
    }

    // ────────────────────────────────────────────────────────────────
    //  Course data model + persistence
    // ────────────────────────────────────────────────────────────────

    private record CourseData(DateTime Start, int Duration, int Extended)
    {
        public int  Total    => Duration + Extended;
        public int  DayNum   => Math.Max(1, (int)(DateTime.Today - Start).TotalDays + 1);
        public bool Active   => Total > 0 && DayNum >= 1 && DayNum <= Total;
        public bool Finished => Total > 0 && DayNum > Total;
        public int  Percent  => Total > 0 ? Math.Clamp((int)Math.Round((double)DayNum / Total * 100), 0, 100) : 0;
    }

    private CourseData? GetCourse(Medication med)
    {
        if (_ctx?.State.RawExtras.TryGetValue("_courses", out var token) != true) return null;
        if (token is not JObject obj) return null;
        var entry = obj[med.Id];
        if (entry == null) return null;

        // Try C# format: {"start": "dd.MM.yyyy", "duration": int, "extended": int}
        DateTime start;
        if (DateTime.TryParseExact(entry["start"]?.ToString(), "dd.MM.yyyy", null,
                System.Globalization.DateTimeStyles.None, out start))
        {
            var duration = entry["duration"]?.Value<int>() ?? ParseCourseDays(med.Course, start);
            var extended = entry["extended"]?.Value<int>() ?? 0;
            return new CourseData(start, duration, extended);
        }

        // Try Python format: {"start_date": "YYYY-MM-DD"}
        if (DateTime.TryParse(entry["start_date"]?.ToString(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out start))
        {
            var extended = entry["extended"]?.Value<int>() ?? 0;
            var duration = entry["duration"]?.Value<int>() ?? ParseCourseDays(med.Course, start);
            return new CourseData(start, duration, extended);
        }

        return null;
    }

    private static int ParseCourseDays(string courseStr, DateTime startDate)
    {
        if (string.IsNullOrWhiteSpace(courseStr)) return 0;
        var s = courseStr.Trim().ToLowerInvariant();
        if (s.Contains("длит") || s.Contains("постоян")) return 0; // бессрочно

        var m = Regex.Match(s, @"(\d+)\s*мес");
        if (m.Success) return (int)(startDate.AddMonths(int.Parse(m.Groups[1].Value)) - startDate).TotalDays;

        m = Regex.Match(s, @"(\d+)\s*(?:год|лет)");
        if (m.Success) return (int)(startDate.AddYears(int.Parse(m.Groups[1].Value)) - startDate).TotalDays;

        m = Regex.Match(s, @"(\d+)\s*нед");
        if (m.Success) return int.Parse(m.Groups[1].Value) * 7;

        m = Regex.Match(s, @"(\d+)(?:-\d+)?\s*дн");
        if (m.Success) return int.Parse(m.Groups[1].Value);

        m = Regex.Match(s, @"\d+");
        if (m.Success && int.TryParse(m.Value, out int v)) return v;

        return 0;
    }

    private void SaveCourse(string medId, DateTime start, int duration, int extended)
    {
        if (_ctx == null) return;
        if (!_ctx.State.RawExtras.TryGetValue("_courses", out var token) || token is not JObject obj)
        {
            obj = new JObject();
            _ctx.State.RawExtras["_courses"] = obj;
        }
        obj[medId] = JToken.FromObject(new
        {
            start_date = start.ToString("yyyy-MM-dd"),
            duration,
            extended,
        });
        _ctx.SaveState();
    }

    // ────────────────────────────────────────────────────────────────
    //  Course dialogs
    // ────────────────────────────────────────────────────────────────

    private void ShowStartCourseDialog(Medication med)
    {
        var win = new Window
        {
            Title                 = Loc.T("dlg_course_start_title"),
            Width                 = 320,
            Height                = 210,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner                 = Window.GetWindow(this),
            ResizeMode            = ResizeMode.NoResize,
            Background            = (Brush)FindResource("BgDarkBrush"),
        };
        var sp = new StackPanel { Margin = new Thickness(16) };

        sp.Children.Add(LabelFor("dlg_course_start_date"));
        var dateBox = new TextBox { Text = DateTime.Today.ToString("dd.MM.yyyy"), Padding = new Thickness(4), Margin = new Thickness(0, 0, 0, 8) };
        sp.Children.Add(dateBox);

        sp.Children.Add(LabelFor("dlg_course_duration"));
        var durBox = new TextBox { Text = "30", Padding = new Thickness(4), Margin = new Thickness(0, 0, 0, 8) };
        sp.Children.Add(durBox);

        var errTb = new TextBlock
        {
            Foreground   = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
            FontSize     = 11,
            TextWrapping = TextWrapping.Wrap,
        };
        sp.Children.Add(errTb);

        sp.Children.Add(MakeDialogButtons(out var okBtn, out var cancelBtn));

        okBtn.Click += (_, _) =>
        {
            if (!DateTime.TryParseExact(dateBox.Text.Trim(), "dd.MM.yyyy", null,
                    System.Globalization.DateTimeStyles.None, out var startDate))
            { errTb.Text = Loc.T("dlg_add_date_invalid"); return; }
            if (!int.TryParse(durBox.Text.Trim(), out var dur) || dur <= 0)
            { errTb.Text = Loc.T("dlg_course_dur_invalid"); return; }
            SaveCourse(med.Id, startDate, dur, 0);
            win.DialogResult = true;
        };
        cancelBtn.Click += (_, _) => win.Close();

        win.Content = sp;
        if (win.ShowDialog() == true) Render();
    }

    private void ShowExtendCourseDialog(Medication med, CourseData course)
    {
        var win = new Window
        {
            Title                 = Loc.T("dlg_course_extend_title"),
            Width                 = 280,
            Height                = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner                 = Window.GetWindow(this),
            ResizeMode            = ResizeMode.NoResize,
            Background            = (Brush)FindResource("BgDarkBrush"),
        };
        var sp = new StackPanel { Margin = new Thickness(16) };

        sp.Children.Add(LabelFor("dlg_course_extend_days"));
        var daysBox = new TextBox { Text = "7", Padding = new Thickness(4), Margin = new Thickness(0, 0, 0, 8) };
        sp.Children.Add(daysBox);

        var errTb = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
            FontSize   = 11,
        };
        sp.Children.Add(errTb);

        sp.Children.Add(MakeDialogButtons(out var okBtn, out var cancelBtn));

        okBtn.Click += (_, _) =>
        {
            if (!int.TryParse(daysBox.Text.Trim(), out var add) || add <= 0)
            { errTb.Text = Loc.T("dlg_course_dur_invalid"); return; }
            SaveCourse(med.Id, course.Start, course.Duration, course.Extended + add);
            win.DialogResult = true;
        };
        cancelBtn.Click += (_, _) => win.Close();

        win.Content = sp;
        if (win.ShowDialog() == true) Render();
    }

    // ────────────────────────────────────────────────────────────────
    //  Stock calculation
    // ────────────────────────────────────────────────────────────────

    // Returns null if key absent, true/false if present.
    private bool? GetPurchased(Medication med)
    {
        if (_ctx == null) return null;
        if (!_ctx.State.RawExtras.TryGetValue("purchased", out var tok)) return null;
        if (tok is not JObject obj) return null;
        var val = obj[med.Id];
        if (val?.Type == JTokenType.Boolean) return val.Value<bool>();
        return null;
    }

    private int GetStock(Medication med)
    {
        if (_ctx == null) return -1;
        if (!_ctx.State.RawExtras.TryGetValue("_finance", out var finToken)) return -1;
        if (finToken is not JObject fin) return -1;
        if (fin["counts"] is not JObject counts) return -1;

        // Match by ID first (Python format), then by name as fallback
        var nameLower = med.Name.Trim().ToLowerInvariant();
        JObject? dateMap = null;
        foreach (var prop in counts.Properties())
        {
            if (prop.Name == med.Id || prop.Name.ToLowerInvariant() == nameLower)
            {
                dateMap = prop.Value as JObject;
                break;
            }
        }
        if (dateMap == null) return -1;

        int totalPurchased = 0;
        foreach (var dp in dateMap.Properties())
        {
            if (dp.Value.Type == JTokenType.Integer) totalPurchased += Math.Max(0, dp.Value.Value<int>());
            else if (int.TryParse(dp.Value.ToString(), out int v)) totalPurchased += Math.Max(0, v);
        }

        // Days this medication was taken
        int takenDays = _ctx.State.Marks
            .Count(kv => kv.Value.TryGetValue(med.Id, out var t) && t);

        return Math.Max(0, totalPurchased - takenDays);
    }

    // ────────────────────────────────────────────────────────────────
    //  URL helpers
    // ────────────────────────────────────────────────────────────────

    private static void OpenSearch(string name)
    {
        var url = "https://www.google.com/search?q=" +
                  Uri.EscapeDataString(name + " инструкция");
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static void OpenPharmacy(Medication med)
    {
        string url;
        if (!string.IsNullOrEmpty(med.Pharmacy) && med.Pharmacy.StartsWith("http"))
            url = med.Pharmacy;
        else
        {
            var q = !string.IsNullOrEmpty(med.Pharmacy) ? med.Pharmacy : med.Name;
            url = "https://tabletki.ua/search/?q=" + Uri.EscapeDataString(q);
        }
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // ────────────────────────────────────────────────────────────────
    //  UI helpers
    // ────────────────────────────────────────────────────────────────

    private Border? MakeCourseBadge(Medication med, CourseData? course)
    {
        if (string.IsNullOrEmpty(med.Course)) return null;
        var cs = med.Course.Trim().ToLowerInvariant();

        string text;
        string color;

        if (cs.Contains("длительно") || cs.Contains("долго"))
        {
            text  = "∞ Длительно";
            color = "#388E3C";   // зелёный
        }
        else if (cs.Contains("потребност") || cs == "sos" || cs.Contains("по требов"))
        {
            text  = "По потреб.";
            color = "#E65100";   // оранжевый
        }
        else if (course?.Active == true)
        {
            text  = ShortenCourse(med.Course);
            color = "#F9A825";   // жёлтый — активный курс
        }
        else
        {
            text  = ShortenCourse(med.Course);
            color = "#546E7A";   // серо-синий — не начатый
        }

        return MakeBadge(text, color);
    }

    private static string ShortenCourse(string course)
    {
        var s = course.Trim();
        s = Regex.Replace(s, @"\bмесяц(?:ев|а|)\b", "мес", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bнедел(?:ь|и|я)\b", "нед", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bдн(?:ей|я|ь)\b",   "дн",  RegexOptions.IgnoreCase);
        return s.Length > 12 ? s[..10] + "…" : s;
    }

    private static Border MakeClickBadge(string label, string hexBg)
        => new()
        {
            Background   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexBg)),
            CornerRadius = new CornerRadius(3),
            Padding      = new Thickness(5, 1, 5, 1),
            Margin       = new Thickness(3, 0, 0, 0),
            Cursor       = Cursors.Hand,
            Child        = new TextBlock { Text = label, FontSize = 10, Foreground = Brushes.White },
            VerticalAlignment = VerticalAlignment.Center,
        };

    private static Border MakeBadge(string label, string hexBg)
        => new()
        {
            Background   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexBg)),
            CornerRadius = new CornerRadius(3),
            Padding      = new Thickness(5, 1, 5, 1),
            Margin       = new Thickness(3, 0, 0, 0),
            Child        = new TextBlock { Text = label, FontSize = 10, Foreground = Brushes.White },
            VerticalAlignment = VerticalAlignment.Center,
        };

    private TextBlock LabelFor(string key)
        => new()
        {
            Text       = Loc.T(key),
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            Margin     = new Thickness(0, 0, 0, 4),
        };

    private static StackPanel MakeDialogButtons(out Button okBtn, out Button cancelBtn)
    {
        okBtn     = new Button { Content = Loc.T("btn_save"),   Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        cancelBtn = new Button { Content = Loc.T("btn_cancel"), Padding = new Thickness(12, 4, 12, 4) };
        return new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(0, 8, 0, 0),
            Children            = { okBtn, cancelBtn },
        };
    }

    // ────────────────────────────────────────────────────────────────
    //  Mark taken / section accent / date nav
    // ────────────────────────────────────────────────────────────────

    private void MarkTaken(Medication med, string iso, bool taken)
    {
        if (_ctx == null) return;
        _ctx.State.SetTaken(iso, med.Id, taken);
        _ctx.SaveState();

        var total = _ctx.Sections.Sum(s => s.Items.Count);
        var done  = _ctx.Sections.Sum(s => s.Items.Count(m => _ctx.State.IsTaken(iso, m.Id)));
        StatsLabel.Text = total == 0 ? "" : Loc.T("schedule_stats",
            ("taken", done), ("total", total));

        _ctx.NotifyChanged();
    }

    private Brush SectionAccent(string key) => key switch
    {
        "morning" => (Brush)FindResource("SectionMorningBrush"),
        "day"     => (Brush)FindResource("SectionDayBrush"),
        "evening" => (Brush)FindResource("SectionEveningBrush"),
        "night"   => (Brush)FindResource("SectionNightBrush"),
        "sos"     => (Brush)FindResource("SectionSosBrush"),
        _         => (Brush)FindResource("AccentBrush"),
    };

    private static int IsoDayOfWeek(DateTime d) => ((int)d.DayOfWeek + 6) % 7;

    private void BtnPrevDay_Click(object sender, RoutedEventArgs e) { _currentDate = _currentDate.AddDays(-1); Render(); }
    private void BtnNextDay_Click(object sender, RoutedEventArgs e) { _currentDate = _currentDate.AddDays( 1); Render(); }
    private void BtnToday_Click(object sender, RoutedEventArgs e)   { _currentDate = DateTime.Today; Render(); }

    private void BtnNow_Click(object sender, RoutedEventArgs e)
    {
        if (_ctx == null) return;
        var nowKey = GetNowSectionKey();
        foreach (var section in _ctx.Sections)
            _collapsed[section.SectionKey] = section.SectionKey != nowKey;
        _currentDate = DateTime.Today;
        Render();
    }

    private static string GetNowSectionKey()
    {
        var t = DateTime.Now.TimeOfDay;
        if (t < TimeSpan.FromHours(14)) return "morning";
        if (t < TimeSpan.FromHours(18)) return "day";
        if (t < TimeSpan.FromHours(23)) return "evening";
        return "night";
    }

    private void BtnManage_Click(object sender, RoutedEventArgs e)
    {
        if (_ctx == null) return;
        var dlg = new ManageMedicationsWindow(_ctx.Sections) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            _ctx.SaveMedications();
            _ctx.NotifyChanged();
        }
    }
}
