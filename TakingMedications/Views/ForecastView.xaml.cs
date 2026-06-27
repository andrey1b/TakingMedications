using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TakingMedications.Common;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class ForecastView : UserControl
{
    private MedAppContext? _ctx;

    public ForecastView()
    {
        InitializeComponent();
        Loc.LanguageChanged    += Render;
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
        RenderForecast();
        RenderShopping();
    }

    // ── Прогноз запаса ───────────────────────────────────────────────

    private void RenderForecast()
    {
        ForecastPanel.Children.Clear();
        LblForecastTitle.Text    = Loc.T("forecast_title");
        LblForecastSubtitle.Text = Loc.T("forecast_subtitle");

        var items = ComputeForecast();
        if (items.Count == 0)
        {
            ForecastPanel.Children.Add(Empty(Loc.T("forecast_no_data")));
            return;
        }

        // Заголовок таблицы
        ForecastPanel.Children.Add(BuildForecastHeader());

        foreach (var item in items.OrderBy(x => x.DaysLeft))
            ForecastPanel.Children.Add(BuildForecastRow(item));
    }

    private record ForecastItem(string Name, int Stock, int DailyNeed, int DaysLeft);

    private List<ForecastItem> ComputeForecast()
    {
        var result = new List<ForecastItem>();
        if (_ctx == null) return result;

        foreach (var section in _ctx.Sections)
        foreach (var med in section.Items)
        {
            var stock = GetStock(med);
            if (stock < 0) continue; // нет данных о запасе

            // Сколько принимается в день (считаем по кратности секций)
            int dailyNeed = 1; // упрощённо: 1 таблетка в день
            int daysLeft  = dailyNeed > 0 ? stock / dailyNeed : 9999;

            result.Add(new ForecastItem(med.Name, stock, dailyNeed, daysLeft));
        }
        return result;
    }

    private int GetStock(Medication med)
    {
        if (!_ctx!.State.RawExtras.TryGetValue("_courses", out var token)) return -1;
        if (token is not Newtonsoft.Json.Linq.JObject courses) return -1;
        if (courses[med.Id] is not Newtonsoft.Json.Linq.JObject c) return -1;
        var countToken = c["count"] ?? c["stock"];
        if (countToken == null) return -1;
        return countToken.ToObject<int>();
    }

    private UIElement BuildForecastHeader()
    {
        var grid = MakeRowGrid(new[] { 1.5, 0.8, 0.8, 1.0 });
        AddCell(grid, 0, Loc.T("forecast_col_name"),   true);
        AddCell(grid, 1, Loc.T("forecast_col_stock"),  true);
        AddCell(grid, 2, Loc.T("forecast_col_daily"),  true);
        AddCell(grid, 3, Loc.T("forecast_col_days"),   true);
        return new Border
        {
            Padding = new Thickness(6, 4, 6, 4),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = (Brush)FindResource("TextSecondaryBrush"),
            Child = grid
        };
    }

    private UIElement BuildForecastRow(ForecastItem item)
    {
        var grid = MakeRowGrid(new[] { 1.5, 0.8, 0.8, 1.0 });

        Brush daysBrush = item.DaysLeft <= 7  ? new SolidColorBrush(Color.FromRgb(0xE7,0x4C,0x3C))
                        : item.DaysLeft <= 14 ? new SolidColorBrush(Color.FromRgb(0xF1,0xC4,0x0F))
                        : (Brush)FindResource("TextPrimaryBrush");

        AddCell(grid, 0, item.Name);
        AddCell(grid, 1, item.Stock.ToString());
        AddCell(grid, 2, item.DailyNeed.ToString());

        var daysCell = new TextBlock
        {
            Text = item.DaysLeft >= 9999 ? "∞" : $"{item.DaysLeft} {Loc.T("forecast_days")}",
            FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = daysBrush, VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(daysCell, 3);
        grid.Children.Add(daysCell);

        return new Border { Padding = new Thickness(6, 5, 6, 5), Child = grid };
    }

    // ── Список покупок ───────────────────────────────────────────────

    private void RenderShopping()
    {
        ShoppingPanel.Children.Clear();
        LblShoppingTitle.Text    = Loc.T("shopping_title");
        LblShoppingSubtitle.Text = Loc.T("shopping_subtitle");

        var items = ComputeForecast()
            .Where(x => x.DaysLeft <= 14)
            .OrderBy(x => x.DaysLeft)
            .ToList();

        if (items.Count == 0)
        {
            ShoppingPanel.Children.Add(Empty(Loc.T("shopping_all_ok")));
            return;
        }

        foreach (var item in items)
            ShoppingPanel.Children.Add(BuildShoppingRow(item));
    }

    private UIElement BuildShoppingRow(ForecastItem item)
    {
        bool urgent = item.DaysLeft <= 0;
        bool warn   = item.DaysLeft <= 7;

        var icon = new TextBlock
        {
            Text = urgent ? "🔴" : warn ? "🟡" : "🟢",
            FontSize = 14, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 8, 0)
        };

        var name = new TextBlock
        {
            Text = item.Name, FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        var daysText = urgent
            ? Loc.T("shopping_depleted")
            : Loc.T("shopping_days_left", ("n", item.DaysLeft));

        var info = new TextBlock
        {
            Text = daysText, FontSize = 11,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        row.Children.Add(icon);
        row.Children.Add(name);
        row.Children.Add(info);

        return new Border
        {
            Padding = new Thickness(6, 4, 6, 4),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = (Brush)FindResource("SeparatorBrush"),
            Child = row
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static Grid MakeRowGrid(double[] ratios)
    {
        var g = new Grid();
        foreach (var r in ratios)
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(r, GridUnitType.Star) });
        return g;
    }

    private void AddCell(Grid g, int col, string text, bool header = false)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = 12,
            FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = header
                ? (Brush)FindResource("TextSecondaryBrush")
                : (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(tb, col);
        g.Children.Add(tb);
    }

    private TextBlock Empty(string text) => new()
    {
        Text = text, FontSize = 13,
        Foreground = (Brush)FindResource("TextSecondaryBrush"),
        Margin = new Thickness(4, 10, 4, 10),
        HorizontalAlignment = HorizontalAlignment.Center
    };
}
