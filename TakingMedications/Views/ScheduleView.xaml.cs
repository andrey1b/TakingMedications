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

        // ── Доп. блоки (порт из Python v64) ──────────────────────────
        SectionsPanel.Children.Add(BuildNotesCard());
        SectionsPanel.Children.Add(BuildShoppingCard());

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

    // ════════════════════════════════════════════════════════════════
    //  Блоки «Важные примечания» и «Необходимо приобрести» (порт из v64)
    // ════════════════════════════════════════════════════════════════

    private const string NotesBlockKey    = "_notes_block";
    private const string ShoppingBlockKey = "_shopping_block";

    private static bool IsRu => Loc.CurrentLang == "ru";
    private static readonly System.Globalization.CultureInfo Inv =
        System.Globalization.CultureInfo.InvariantCulture;

    // Русское склонение: 1 пункт / 2-4 пункта / 5+ пунктов
    private static string RuPlural(int n, string one, string few, string many)
    {
        int a = Math.Abs(n), last = a % 10, last2 = a % 100;
        if (last2 is >= 11 and <= 14) return many;
        if (last == 1) return one;
        if (last is >= 2 and <= 4) return few;
        return many;
    }

    private Border CardBorder(UIElement child) => new Border
    {
        Background      = (Brush)FindResource("BgCardBrush"),
        BorderBrush     = (Brush)FindResource("CardBorderBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius    = new CornerRadius(12),
        Padding         = new Thickness(14, 12, 14, 12),
        Margin          = new Thickness(0, 0, 0, 10),
        Child           = child,
    };

    // ── «Важные примечания» — по умолчанию свёрнут ───────────────────
    private Border BuildNotesCard()
    {
        var accent = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)); // красный (как SOS)
        bool collapsed = _collapsed.GetValueOrDefault(NotesBlockKey, true);
        var notes = GetImportantNotes();
        int n = notes.Count;

        var indicator = new TextBlock
        {
            Text = collapsed ? "▶" : "▼", FontSize = 11, Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
        };
        var title = new TextBlock
        {
            Text = Loc.T("notes_header"), FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = accent, VerticalAlignment = VerticalAlignment.Center,
        };
        string countWord = IsRu ? RuPlural(n, "пункт", "пункта", "пунктов") : (n == 1 ? "item" : "items");
        var count = new TextBlock
        {
            Text = $"  ({n} {countWord})", FontSize = 13, Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var left = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(indicator);
        left.Children.Add(title);
        left.Children.Add(count);

        var editBadge = MakeClickBadge("✎");
        editBadge.HorizontalAlignment = HorizontalAlignment.Right;
        editBadge.MouseLeftButtonUp += (_, _) => ShowNotesEditor();

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(left, 0);      header.Children.Add(left);
        Grid.SetColumn(editBadge, 1); header.Children.Add(editBadge);

        var content = new StackPanel
        {
            Margin     = new Thickness(0, 8, 0, 0),
            Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible,
        };
        if (n > 0)
        {
            for (int i = 0; i < notes.Count; i++)
                content.Children.Add(new TextBlock
                {
                    Text         = $"{i + 1}. {notes[i]}",
                    FontSize     = 13,
                    Foreground   = (Brush)FindResource("TextPrimaryBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin       = new Thickness(0, 2, 0, 2),
                });
        }
        else
        {
            content.Children.Add(new TextBlock
            {
                Text         = Loc.T("notes_empty_hint"),
                FontSize     = 12, FontStyle = FontStyles.Italic,
                Foreground   = (Brush)FindResource("TextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        left.MouseLeftButtonUp += (_, _) =>
        {
            bool now = !_collapsed.GetValueOrDefault(NotesBlockKey, true);
            _collapsed[NotesBlockKey] = now;
            indicator.Text     = now ? "▶" : "▼";
            content.Visibility = now ? Visibility.Collapsed : Visibility.Visible;
            PersistCollapseState(NotesBlockKey, now);
        };

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(content);
        return CardBorder(stack);
    }

    // ── «Необходимо приобрести» — по умолчанию развёрнут ─────────────
    private Border BuildShoppingCard()
    {
        var orange    = new SolidColorBrush(Color.FromRgb(0xEF, 0x6C, 0x00));
        var red       = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
        var lowOrange = new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00));
        bool collapsed = _collapsed.GetValueOrDefault(ShoppingBlockKey, false);
        string cur = IsRu ? "грн" : "UAH";

        var items = ComputeShoppingList();

        var indicator = new TextBlock
        {
            Text = collapsed ? "▶" : "▼", FontSize = 11, Foreground = orange,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
        };
        var title = new TextBlock
        {
            Text = Loc.T("purchase_header"), FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = orange, VerticalAlignment = VerticalAlignment.Center,
        };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(indicator);
        left.Children.Add(title);

        var countTb = new TextBlock
        {
            FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = orange,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
        };

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(left, 0);    header.Children.Add(left);
        Grid.SetColumn(countTb, 1); header.Children.Add(countTb);

        var content = new StackPanel
        {
            Margin     = new Thickness(0, 8, 0, 0),
            Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible,
        };

        if (items.Count == 0)
        {
            content.Children.Add(new TextBlock
            {
                Text       = Loc.T("purchase_all_ok"),
                FontSize   = 12, FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
                TextWrapping = TextWrapping.Wrap,
            });
            countTb.Text = "";
        }
        else
        {
            double total = 0; bool hasPrice = false;
            string rate = IsRu ? "расход" : "use";
            string dayW = IsRu ? "день"   : "day";
            var clip = new List<string> { $"{Loc.T("purchase_header")} ({DateTime.Now:dd.MM.yyyy}):", "" };

            for (int i = 0; i < items.Count; i++)
            {
                var (name, rem, daily) = items[i];
                string remText, daysText = "";
                Brush col;
                if (rem <= 0)
                {
                    col = red;
                    remText = rem == 0
                        ? (IsRu ? "закончились" : "out of stock")
                        : (IsRu ? $"перерасход {-rem} шт" : $"overused by {-rem} pcs");
                }
                else
                {
                    col = lowOrange;
                    remText = IsRu
                        ? $"осталось {rem} {RuPlural(rem, "таблетка", "таблетки", "таблеток")}"
                        : $"{rem} {(rem == 1 ? "tablet" : "tablets")} left";
                    int daysLeft = daily > 0 ? rem / daily : 0;
                    daysText = IsRu
                        ? $"  (~{daysLeft} {RuPlural(daysLeft, "день", "дня", "дней")})"
                        : $"  (~{daysLeft} {(daysLeft == 1 ? "day" : "days")})";
                }

                double? price = GetLastPriceForName(name);
                string priceText = "";
                if (price.HasValue)
                {
                    hasPrice = true; total += price.Value;
                    priceText = $"  ·  {price.Value.ToString("F2", Inv)} {cur}";
                }

                content.Children.Add(new TextBlock
                {
                    Text         = $"  {i + 1}. {name} — {remText}{daysText}  ({rate}: {daily}/{dayW}){priceText}",
                    FontSize     = 13, FontWeight = FontWeights.SemiBold,
                    Foreground   = col, TextWrapping = TextWrapping.Wrap,
                    Margin       = new Thickness(0, 2, 0, 2),
                });

                clip.Add($"{i + 1}. {name} — {remText}{daysText}  ({rate}: {daily}/{dayW})"
                         + (price.HasValue ? $"  / {price.Value.ToString("F2", Inv)} {cur}" : ""));
            }

            if (hasPrice)
            {
                content.Children.Add(new Rectangle
                {
                    Height = 1, Fill = (Brush)FindResource("SeparatorBrush"),
                    Margin = new Thickness(0, 5, 0, 4),
                });
                string totalW = IsRu ? "Итого" : "Total";
                content.Children.Add(new TextBlock
                {
                    Text       = $"{totalW}: {total.ToString("F2", Inv)} {cur}",
                    FontSize   = 14, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0x36, 0x0C)),
                    Margin     = new Thickness(0, 2, 0, 2),
                });
                clip.Add("");
                clip.Add($"{(IsRu ? "ИТОГО" : "TOTAL")}: {total.ToString("F2", Inv)} {cur}");
                countTb.Text = $"{items.Count}  ·  {total.ToString("F2", Inv)} {cur}";
            }
            else
            {
                countTb.Text = $"{items.Count}";
            }

            var copyBadge = MakeClickBadge(Loc.T("btn_copy_clipboard"), "#FF8F00");
            copyBadge.HorizontalAlignment = HorizontalAlignment.Left;
            copyBadge.Margin = new Thickness(0, 8, 0, 0);
            var clipText = string.Join("\n", clip);
            copyBadge.MouseLeftButtonUp += (_, _) =>
            {
                try { System.Windows.Clipboard.SetText(clipText); } catch { }
                ((TextBlock)copyBadge.Child).Text = Loc.T("purchase_copied");
            };
            content.Children.Add(copyBadge);
        }

        left.MouseLeftButtonUp += (_, _) =>
        {
            bool now = !_collapsed.GetValueOrDefault(ShoppingBlockKey, false);
            _collapsed[ShoppingBlockKey] = now;
            indicator.Text     = now ? "▶" : "▼";
            content.Visibility = now ? Visibility.Collapsed : Visibility.Visible;
            PersistCollapseState(ShoppingBlockKey, now);
        };

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(content);
        return CardBorder(stack);
    }

    // ── Расчёт списка покупок (порт med_shopping.compute_shopping_list) ──
    private List<(string Name, int Remaining, int Daily)> ComputeShoppingList()
    {
        var daily       = new Dictionary<string, int>();
        var nameToMeds  = new Dictionary<string, List<Medication>>();
        foreach (var sec in _ctx!.Sections)
            foreach (var m in sec.Items)
            {
                var nm = (m.Name ?? "").Trim();
                if (nm.Length == 0) continue;
                daily[nm] = daily.GetValueOrDefault(nm) + 1;
                if (!nameToMeds.TryGetValue(nm, out var lst)) nameToMeds[nm] = lst = new();
                lst.Add(m);
            }

        var result = new List<(string, int, int)>();
        foreach (var (name, d) in daily)
        {
            var meds = nameToMeds[name];
            bool allExpired = meds.Count > 0 && meds.All(m => GetCourse(m)?.Finished == true);
            if (allExpired) continue;
            var (remaining, hasData) = ComputeNameStock(name);
            if (!hasData) continue;
            if (remaining <= d) result.Add((name, remaining, d));
        }
        // Сначала закончившиеся / в минус, затем по алфавиту
        result.Sort((a, b) => a.Item2 != b.Item2
            ? a.Item2.CompareTo(b.Item2)
            : string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    // Запас по НАЗВАНИЮ (суммируя покупки и приёмы по всем mid с этим именем).
    // remaining не ограничен снизу (может быть отрицательным = перерасход).
    private (int Remaining, bool HasData) ComputeNameStock(string name)
    {
        var nameKey = (name ?? "").Trim().ToLowerInvariant();
        if (nameKey.Length == 0) return (0, false);

        var sameMids = _ctx!.Sections.SelectMany(s => s.Items)
            .Where(m => m.Name.Trim().ToLowerInvariant() == nameKey)
            .Select(m => m.Id).ToHashSet();
        if (sameMids.Count == 0) return (0, false);

        int purchased = 0;
        if (_ctx.State.RawExtras.TryGetValue("_finance", out var finTok) && finTok is JObject fin
            && fin["counts"] is JObject counts)
        {
            foreach (var prop in counts.Properties())
            {
                if (!sameMids.Contains(prop.Name) || prop.Value is not JObject dateMap) continue;
                foreach (var dp in dateMap.Properties())
                    if (int.TryParse(dp.Value.ToString(), out int v)) purchased += v;
            }
        }
        if (purchased <= 0) return (0, false);

        int taken = 0;
        foreach (var kv in _ctx.State.Marks)
            foreach (var mid in sameMids)
                if (kv.Value.TryGetValue(mid, out var t) && t) taken++;

        return (purchased - taken, true);
    }

    // Последняя ненулевая цена препарата из «Финансов» (по всем mid с этим именем).
    private double? GetLastPriceForName(string name)
    {
        var nameKey = (name ?? "").Trim().ToLowerInvariant();
        if (nameKey.Length == 0) return null;

        var sameMids = _ctx!.Sections.SelectMany(s => s.Items)
            .Where(m => m.Name.Trim().ToLowerInvariant() == nameKey)
            .Select(m => m.Id).ToHashSet();
        if (sameMids.Count == 0) return null;

        if (_ctx.State.RawExtras.TryGetValue("_finance", out var finTok) && finTok is JObject fin
            && fin["dates"] is JArray dates && fin["cells"] is JObject cells)
        {
            for (int i = dates.Count - 1; i >= 0; i--)
            {
                var date = dates[i].ToString();
                foreach (var mid in sameMids)
                {
                    if (cells[mid] is JObject cm && cm[date] is JToken cell)
                    {
                        double vf = ParseNum(cell);
                        if (vf > 0) return vf;
                    }
                }
            }
        }
        return null;
    }

    // Безопасный разбор числа из JSON-ячейки, НЕ завязанный на текущую культуру.
    // Числовой токен берём напрямую (иначе ToString() на ru-локали даёт "50,4",
    // и парс с AllowThousands превращает его в 504). Строку нормализуем , → .
    private static double ParseNum(JToken? t)
    {
        if (t == null) return 0;
        if (t.Type is JTokenType.Float or JTokenType.Integer) return t.Value<double>();
        var s = (t.ToString() ?? "").Trim().Replace(',', '.');
        return double.TryParse(s, System.Globalization.NumberStyles.Float, Inv, out var v) ? v : 0;
    }

    // ── Важные примечания: чтение/запись (Python-ключ _important_notes) ──
    private List<string> GetImportantNotes()
    {
        var list = new List<string>();
        if (_ctx?.State.RawExtras.TryGetValue("_important_notes", out var tok) == true && tok is JArray arr)
            foreach (var t in arr)
            {
                var s = t?.ToString();
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
            }
        return list;
    }

    private void SaveImportantNotes(IEnumerable<string> notes)
    {
        if (_ctx == null) return;
        var cleaned = notes.Select(s => s.Trim()).Where(s => s.Length > 0).Cast<object>().ToArray();
        _ctx.State.RawExtras["_important_notes"] = new JArray(cleaned);
        _ctx.SaveState();
    }

    // Простой редактор примечаний — одно на строку (порт NotesEditDialog).
    private void ShowNotesEditor()
    {
        var win = new Window
        {
            Title                 = Loc.T("notes_header"),
            Width                 = 560, Height = 460,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner                 = Window.GetWindow(this),
            Background            = (Brush)FindResource("BgDarkBrush"),
        };
        var root = new DockPanel { Margin = new Thickness(16) };

        var hint = new TextBlock
        {
            Text       = IsRu ? "Каждое важное примечание — с новой строки:" : "One note per line:",
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            Margin     = new Thickness(0, 0, 0, 8),
        };
        DockPanel.SetDock(hint, Dock.Top);
        root.Children.Add(hint);

        var buttons = MakeDialogButtons(out var okBtn, out var cancelBtn);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        var tb = new TextBox
        {
            AcceptsReturn            = true,
            TextWrapping             = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text                     = string.Join("\n", GetImportantNotes()),
            FontSize                 = 13,
            Padding                  = new Thickness(6),
            Margin                   = new Thickness(0, 0, 0, 8),
        };
        root.Children.Add(tb);

        okBtn.Click += (_, _) =>
        {
            var lines = tb.Text.Replace("\r", "").Split('\n');
            SaveImportantNotes(lines);
            win.DialogResult = true;
        };
        cancelBtn.Click += (_, _) => win.Close();

        win.Content = root;
        if (win.ShowDialog() == true) Render();
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
            var showBtn = MakeClickBadge(Loc.T("course_show_btn"));
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
                Loc.T("schedule_doctor", ("doctor", med.Doctor))));

        if (!string.IsNullOrEmpty(med.Pharmacy))
        {
            var pharmBtn = MakeClickBadge(Loc.T("btn_pharmacy"));
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
            var descBtn   = MakeClickBadge(Loc.T("btn_description"));
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
            var extendBtn = MakeClickBadge(Loc.T("course_extend_btn"));
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
            var newBtn = MakeClickBadge(Loc.T("course_start_btn"));
            newBtn.MouseLeftButtonUp += (_, _) => ShowStartCourseDialog(med);
            panel.Children.Add(newBtn);
            var hideBtn = MakeClickBadge(Loc.T("course_hide_btn"));
            hideBtn.MouseLeftButtonUp += (_, _) => ToggleCourseExpand(med.Id, null, null);
            panel.Children.Add(hideBtn);
            return panel;
        }

        // ── Курс ещё не начат ────────────────────────────────────────
        var startBtn = MakeClickBadge(Loc.T("course_start_btn"));
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

        if (cs.Contains("длительно") || cs.Contains("долго"))
            return MakeBadge("∞ Длительно");                     // AccentBrush
        if (cs.Contains("потребност") || cs == "sos" || cs.Contains("по требов"))
            return MakeBadge("По потреб.", "#E65100");            // семантический оранжевый
        return MakeBadge(ShortenCourse(med.Course), (Brush)FindResource("HeaderButtonBrush")); // как кнопка Настройки
    }

    private static string ShortenCourse(string course)
    {
        var s = course.Trim();
        s = Regex.Replace(s, @"\bмесяц(?:ев|а|)\b", "мес", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bнедел(?:ь|и|я)\b", "нед", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bдн(?:ей|я|ь)\b",   "дн",  RegexOptions.IgnoreCase);
        return s.Length > 12 ? s[..10] + "…" : s;
    }

    private Border MakeClickBadge(string label, string? hexBg = null)
    {
        var bg = hexBg is not null
            ? (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexBg))
            : (Brush)FindResource("AccentBrush");
        return new Border
        {
            Background        = bg,
            CornerRadius      = new CornerRadius(4),
            Padding           = new Thickness(7, 2, 7, 2),
            Margin            = new Thickness(3, 0, 0, 0),
            Cursor            = Cursors.Hand,
            Child             = new TextBlock { Text = label, FontSize = 12, Foreground = Brushes.White },
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private Border MakeBadge(string label, string? hexBg = null)
    {
        var bg = hexBg is not null
            ? (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexBg))
            : (Brush)FindResource("AccentBrush");
        return MakeBadge(label, bg);
    }

    private Border MakeBadge(string label, Brush brush)
        => new Border
        {
            Background        = brush,
            CornerRadius      = new CornerRadius(4),
            Padding           = new Thickness(7, 2, 7, 2),
            Margin            = new Thickness(3, 0, 0, 0),
            Child             = new TextBlock { Text = label, FontSize = 12, Foreground = Brushes.White },
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
