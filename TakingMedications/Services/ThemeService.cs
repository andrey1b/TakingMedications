using System;
using System.Windows;
using System.Windows.Media;

namespace TakingMedications.Services;

/// <summary>
/// Переключение тёмной / светлой темы в рантайме.
/// Мутирует <see cref="SolidColorBrush"/> в Application.Resources —
/// все StaticResource автоматически перерисовываются.
/// </summary>
public static class ThemeService
{
    public const string Dark  = "dark";
    public const string Light = "light";

    public static string Current { get; private set; } = Dark;
    public static event Action? ThemeChanged;

    private static readonly (string Key, Color DarkColor, Color LightColor)[] _brushes =
    [
        // Фоны и текст
        // Светлая тема — палитра GardenPlanner (#2C5F2D)
        ("BgDarkBrush",          Color.FromRgb(0x1A, 0x1A, 0x2E), Color.FromRgb(0xF4, 0xF8, 0xF4)),
        ("BgCardBrush",          Color.FromRgb(0x16, 0x21, 0x3E), Color.FromRgb(0xFF, 0xFF, 0xFF)),
        ("BgInputBrush",         Color.FromRgb(0x0F, 0x34, 0x60), Color.FromRgb(0xEE, 0xF5, 0xEE)),
        ("TextPrimaryBrush",     Color.FromRgb(0xEA, 0xEA, 0xEA), Color.FromRgb(0x2B, 0x2B, 0x2B)),
        ("TextSecondaryBrush",   Color.FromRgb(0xA0, 0xA0, 0xB8), Color.FromRgb(0x4F, 0x5A, 0x41)),
        // Акцент (кнопки) — зелёный GardenPlanner в светлой теме
        ("AccentBrush",          Color.FromRgb(0x6C, 0x63, 0xFF), Color.FromRgb(0x2C, 0x5F, 0x2D)),
        ("AccentHoverBrush",     Color.FromRgb(0x5A, 0x52, 0xD5), Color.FromRgb(0x23, 0x4C, 0x24)),
        // Цвет текста курса (жёлтый в тёмной / тёмно-зелёный в светлой)
        ("CourseBrush",          Color.FromRgb(0xFF, 0xD5, 0x4F), Color.FromRgb(0x1B, 0x5E, 0x20)),
        // Разделитель между строками
        ("SeparatorBrush",       Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), Color.FromRgb(0xC8, 0xD8, 0xC8)),
        // Рамка карточки секции
        ("CardBorderBrush",      Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), Color.FromRgb(0xB8, 0xCC, 0xB8)),
        // Секции (смысловые цвета времени суток — одинаковы в обеих темах)
        ("SectionMorningBrush",  Color.FromRgb(0xF1, 0xC4, 0x0F), Color.FromRgb(0x7B, 0x62, 0x00)),
        ("SectionDayBrush",      Color.FromRgb(0x34, 0x98, 0xDB), Color.FromRgb(0x0C, 0x4F, 0x8A)),
        ("SectionEveningBrush",  Color.FromRgb(0x9B, 0x59, 0xB6), Color.FromRgb(0x5B, 0x21, 0x86)),
        ("SectionNightBrush",    Color.FromRgb(0x34, 0x49, 0x5E), Color.FromRgb(0x1A, 0x25, 0x30)),
        ("SectionSosBrush",      Color.FromRgb(0xE7, 0x4C, 0x3C), Color.FromRgb(0xA0, 0x1B, 0x0D)),
    ];

    /// <summary>Применяет пресет цвета разделителей. Не зависит от темы.</summary>
    public static void ApplyBorderPreset(string preset)
    {
        var (sep, card) = preset switch
        {
            "light"  => (Color.FromRgb(0xD8, 0xEB, 0xD8), Color.FromRgb(0xC8, 0xDE, 0xC8)),
            "dark"   => (Color.FromRgb(0xA8, 0xC0, 0xA8), Color.FromRgb(0x98, 0xB4, 0x98)),
            _        => (Color.FromRgb(0xC8, 0xD8, 0xC8), Color.FromRgb(0xB8, 0xCC, 0xB8)), // medium
        };
        var res = Application.Current.Resources;
        res["SeparatorBrush"]  = new SolidColorBrush(sep);
        res["CardBorderBrush"] = new SolidColorBrush(card);
    }

    public static void SetTheme(string theme)
    {
        Current = theme == Light ? Light : Dark;
        bool isLight = Current == Light;
        var res = Application.Current.Resources;

        foreach (var (key, dark, light) in _brushes)
            res[key] = new SolidColorBrush(isLight ? light : dark);

        ThemeChanged?.Invoke();
    }
}
