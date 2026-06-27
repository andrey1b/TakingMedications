using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TakingMedications.Common;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class NotesDialog : Window
{
    private readonly MedAppContext _ctx;

    public NotesDialog(MedAppContext ctx)
    {
        InitializeComponent();
        _ctx = ctx;

        ApplyLocalization();
        Loc.LanguageChanged += ApplyLocalization;
        Closed += (_, _) => Loc.LanguageChanged -= ApplyLocalization;

        Render();
    }

    // ── Localization ──────────────────────────────────────────────────────

    private void ApplyLocalization()
    {
        Title            = Loc.T("notes_title");
        HeaderLabel.Text = Loc.T("notes_title");
        LblColor.Text    = Loc.T("notes_color_none") + ":";
        BtnAdd.Content   = Loc.T("btn_add");

        NewNoteBox.Tag = Loc.T("notes_add_placeholder");
        if (string.IsNullOrEmpty(NewNoteBox.Text))
            SetPlaceholder(true);

        Render();
    }

    // ── Rendering ─────────────────────────────────────────────────────────

    private void Render()
    {
        NotesList.Children.Clear();

        if (_ctx.State.Notes.Count == 0)
        {
            NotesList.Children.Add(new TextBlock
            {
                Text       = Loc.T("notes_empty"),
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                FontSize   = 14,
                Margin     = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            return;
        }

        for (int i = _ctx.State.Notes.Count - 1; i >= 0; i--)
        {
            var note = _ctx.State.Notes[i];
            NotesList.Children.Add(BuildNoteCard(note, i));
        }
    }

    private UIElement BuildNoteCard(NoteEntry note, int index)
    {
        var accentColor = ColorFromNoteColor(note.Color);

        var card = new Border
        {
            Background      = (Brush)FindResource("BgCardBrush"),
            BorderBrush     = (Brush)FindResource("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Margin          = new Thickness(0, 0, 0, 8),
            Padding         = new Thickness(0),
            ClipToBounds    = true,
        };

        var inner = new Grid();
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Color stripe
        var stripe = new Border
        {
            Background = new SolidColorBrush(accentColor),
        };
        Grid.SetColumn(stripe, 0);
        inner.Children.Add(stripe);

        // Text + date
        var textBlock = new TextBlock
        {
            Text           = note.Text,
            TextWrapping   = TextWrapping.Wrap,
            Foreground     = (Brush)FindResource("TextPrimaryBrush"),
            FontSize       = 13,
            Margin         = new Thickness(10, 8, 8, 4),
        };
        var dateBlock = new TextBlock
        {
            Text           = note.Created,
            Foreground     = (Brush)FindResource("TextSecondaryBrush"),
            FontSize       = 11,
            Margin         = new Thickness(10, 0, 8, 6),
        };
        var textPanel = new StackPanel();
        textPanel.Children.Add(textBlock);
        textPanel.Children.Add(dateBlock);
        Grid.SetColumn(textPanel, 1);
        inner.Children.Add(textPanel);

        // Delete button
        var del = new Button
        {
            Content         = "✕",
            FontSize        = 12,
            Foreground      = (Brush)FindResource("TextSecondaryBrush"),
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding         = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor          = Cursors.Hand,
            Tag             = index,
        };
        del.Click += DeleteNote_Click;
        Grid.SetColumn(del, 2);
        inner.Children.Add(del);

        card.Child = inner;
        return card;
    }

    private static Color ColorFromNoteColor(string? color) => color switch
    {
        "yellow" => (Color)ColorConverter.ConvertFromString("#F9A825"),
        "green"  => (Color)ColorConverter.ConvertFromString("#388E3C"),
        "red"    => (Color)ColorConverter.ConvertFromString("#C62828"),
        "blue"   => (Color)ColorConverter.ConvertFromString("#1565C0"),
        _        => (Color)ColorConverter.ConvertFromString("#546E7A"),
    };

    // ── Event handlers ────────────────────────────────────────────────────

    private void BtnAdd_Click(object sender, RoutedEventArgs e) => AddNote();

    private void NewNoteBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            AddNote();
            e.Handled = true;
        }
    }

    private void AddNote()
    {
        var text = NewNoteBox.Text.Trim();
        if (string.IsNullOrEmpty(text) || text == NewNoteBox.Tag as string) return;

        var color = GetSelectedColor();

        _ctx.State.Notes.Add(new NoteEntry
        {
            Text    = text,
            Created = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            Color   = string.IsNullOrEmpty(color) ? null : color,
        });
        _ctx.SaveState();

        NewNoteBox.Text = "";
        RbColorNone.IsChecked = true;
        Render();
    }

    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is int index)
        {
            var result = MessageBox.Show(
                Loc.T("notes_delete_confirm"),
                Loc.T("confirm_title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            _ctx.State.Notes.RemoveAt(index);
            _ctx.SaveState();
            Render();
        }
    }

    private string GetSelectedColor()
    {
        foreach (var rb in new[] { RbColorNone, RbColorYellow, RbColorGreen, RbColorRed, RbColorBlue })
            if (rb.IsChecked == true) return rb.Tag as string ?? "";
        return "";
    }

    // ── Placeholder helpers ───────────────────────────────────────────────

    private void SetPlaceholder(bool on)
    {
        if (on)
        {
            NewNoteBox.Text       = NewNoteBox.Tag as string ?? "";
            NewNoteBox.Foreground = (Brush)FindResource("TextSecondaryBrush");
        }
        else
        {
            NewNoteBox.Foreground = (Brush)FindResource("TextPrimaryBrush");
        }
    }

    private void NewNoteBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (NewNoteBox.Text == NewNoteBox.Tag as string)
        {
            NewNoteBox.Text = "";
            NewNoteBox.Foreground = (Brush)FindResource("TextPrimaryBrush");
        }
    }

    private void NewNoteBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewNoteBox.Text))
            SetPlaceholder(true);
    }
}
