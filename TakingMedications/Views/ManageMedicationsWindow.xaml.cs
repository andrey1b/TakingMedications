using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class ManageMedicationsWindow : Window
{
    private readonly List<MedicationSection> _sections;
    private bool _dirty;

    public ManageMedicationsWindow(List<MedicationSection> sections)
    {
        InitializeComponent();
        _sections = sections;
        ApplyLocalization();
        Loc.LanguageChanged += OnLangChanged;
        Closed += (_, _) => Loc.LanguageChanged -= OnLangChanged;
        Render();
    }

    private void OnLangChanged()
    {
        ApplyLocalization();
        Render();
    }

    private void ApplyLocalization()
    {
        Title = Loc.T("manage_title");
        HeaderLabel.Text = Loc.T("manage_title");
        BtnInteractions.Content = Loc.T("btn_interactions");
        BtnClose.Content = Loc.T("btn_close");
    }

    private void Render()
    {
        SectionsPanel.Children.Clear();
        foreach (var section in _sections)
            SectionsPanel.Children.Add(BuildSectionBlock(section));
    }

    private Border BuildSectionBlock(MedicationSection section)
    {
        var stack = new StackPanel();

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new TextBlock
        {
            Text = section.Title,
            FontSize = 15, FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var addBtn = new Button
        {
            Content = Loc.T("btn_add"),
            Style = (Style)FindResource("RoundButton"),
            Background = (Brush)FindResource("SuccessBrush"),
            FontSize = 12,
            Padding = new Thickness(10, 4, 10, 4)
        };
        addBtn.Click += (_, _) => AddItem(section);
        Grid.SetColumn(addBtn, 1);
        header.Children.Add(addBtn);

        stack.Children.Add(header);

        if (section.Items.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = Loc.T("empty"),
                FontSize = 12,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(4, 6, 0, 4)
            });
        }
        else
        {
            foreach (var med in section.Items.ToList())
                stack.Children.Add(BuildItemRow(section, med));
        }

        return new Border
        {
            Background = (Brush)FindResource("BgCardBrush"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 10),
            Child = stack
        };
    }

    private Border BuildItemRow(MedicationSection section, Medication med)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var time = new TextBlock
        {
            Text = med.Time,
            FontSize = 13,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(time, 0);
        grid.Children.Add(time);

        var name = new TextBlock
        {
            Text = string.IsNullOrEmpty(med.Subtitle) ? med.Name : $"{med.Name}  ·  {med.Subtitle}",
            FontSize = 13,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var edit = new Button
        {
            Content = Loc.T("btn_edit"),
            Style = (Style)FindResource("RoundButton"),
            Background = (Brush)FindResource("AccentBrush"),
            FontSize = 11,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(8, 0, 6, 0)
        };
        edit.Click += (_, _) => EditItem(section, med);
        Grid.SetColumn(edit, 2);
        grid.Children.Add(edit);

        var del = new Button
        {
            Content = "✕",
            Style = (Style)FindResource("RoundButton"),
            Background = (Brush)FindResource("DangerBrush"),
            FontSize = 11,
            Padding = new Thickness(8, 3, 8, 3)
        };
        del.Click += (_, _) => DeleteItem(section, med);
        Grid.SetColumn(del, 3);
        grid.Children.Add(del);

        return new Border
        {
            Padding = new Thickness(0, 4, 0, 4),
            Child = grid
        };
    }

    private void AddItem(MedicationSection section)
    {
        var med = new Medication { SectionKey = section.SectionKey };
        var dlg = new MedicationEditWindow(med, _sections, isNew: true) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            EnsureUniqueId(med);
            var target = _sections.FirstOrDefault(s => s.SectionKey == med.SectionKey) ?? section;
            target.Items.Add(med);
            _dirty = true;
            Render();
        }
    }

    private void EditItem(MedicationSection section, Medication med)
    {
        var dlg = new MedicationEditWindow(med, _sections, isNew: false) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            // Если пользователь сменил секцию — переносим
            if (med.SectionKey != section.SectionKey)
            {
                section.Items.Remove(med);
                var target = _sections.FirstOrDefault(s => s.SectionKey == med.SectionKey);
                if (target != null) target.Items.Add(med);
                else section.Items.Add(med); // fallback
            }
            _dirty = true;
            Render();
        }
    }

    private void DeleteItem(MedicationSection section, Medication med)
    {
        var ans = MessageBox.Show(
            this,
            Loc.T("manage_delete_confirm", ("name", med.Name)),
            Loc.T("confirm_title"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ans != MessageBoxResult.Yes) return;

        section.Items.Remove(med);
        _dirty = true;
        Render();
    }

    private void EnsureUniqueId(Medication med)
    {
        if (string.IsNullOrWhiteSpace(med.Id)) med.Id = Slug(med.Name);
        if (string.IsNullOrWhiteSpace(med.Id)) med.Id = "med";

        var existing = new HashSet<string>(_sections.SelectMany(s => s.Items).Select(m => m.Id));
        var baseId = med.Id;
        var n = 1;
        while (existing.Contains(med.Id)) med.Id = $"{baseId}{++n}";
    }

    private static string Slug(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var ch in name.ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(ch);
            else if (Cyrillic2Latin.TryGetValue(ch, out var rep)) sb.Append(rep);
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }
        return sb.ToString().Trim('_');
    }

    private static readonly Dictionary<char, string> Cyrillic2Latin = new()
    {
        ['а']="a",['б']="b",['в']="v",['г']="g",['д']="d",['е']="e",['ё']="e",
        ['ж']="zh",['з']="z",['и']="i",['й']="y",['к']="k",['л']="l",['м']="m",
        ['н']="n",['о']="o",['п']="p",['р']="r",['с']="s",['т']="t",['у']="u",
        ['ф']="f",['х']="h",['ц']="c",['ч']="ch",['ш']="sh",['щ']="sch",
        ['ъ']="",['ы']="y",['ь']="",['э']="e",['ю']="yu",['я']="ya",
    };

    private void BtnInteractions_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InteractionsDialog(_sections) { Owner = this };
        dlg.ShowDialog();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _dirty;
        Close();
    }
}
