using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Brush   = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace TakingMedications.Views;

// Строка вкладки «Спросить у ИИ» для WPF ItemsControl.
// Статика (Name/Url/ApiId/цвет) задаётся при создании; динамика (ответ, отметка, подписи) — биндингом.
internal sealed class AiRow : INotifyPropertyChanged
{
    public string Name  { get; init; } = "";
    public string Url   { get; init; } = "";
    public string ApiId { get; init; } = "";
    public Brush  HeaderBrush { get; init; } = Brushes.Gray;

    private bool _enabled;
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    private string _response = "";
    public string Response
    {
        get => _response;
        set { if (Set(ref _response, value)) UpdateStats(); }
    }

    private Brush _responseBrush = Brushes.Gray;
    public Brush ResponseBrush { get => _responseBrush; set => Set(ref _responseBrush, value); }

    private string _stats = "";
    public string Stats { get => _stats; private set => Set(ref _stats, value); }

    private string _saveLabel = "💾";
    public string SaveLabel { get => _saveLabel; set => Set(ref _saveLabel, value); }

    private string _copyLabel = "📋";
    public string CopyLabel { get => _copyLabel; set => Set(ref _copyLabel, value); }

    public Func<int, int, string>? StatsFormat { get; set; }

    private void UpdateStats()
    {
        string t = _response ?? "";
        int chars = t.Length;
        int words = string.IsNullOrWhiteSpace(t)
            ? 0
            : t.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        Stats = StatsFormat?.Invoke(chars, words) ?? $"{chars} / {words}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
