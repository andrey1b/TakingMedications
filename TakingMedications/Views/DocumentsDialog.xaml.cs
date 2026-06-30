using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TakingMedications.Common;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class DocumentsDialog : Window
{
    private readonly string _docsFolder;

    private static readonly string[] ImageExts = { ".jpg",".jpeg",".png",".bmp",".gif",".tiff",".webp" };
    private static readonly string[] DicomExts = { ".dcm",".dicom" };

    public DocumentsDialog(MedAppContext ctx)
    {
        InitializeComponent();
        _docsFolder = AppPaths.ResolveDocumentsDir();

        ApplyLocalization();
        Loc.LanguageChanged += ApplyLocalization;
        Closed += (_, _) => Loc.LanguageChanged -= ApplyLocalization;

        Render();
    }

    private void ApplyLocalization()
    {
        Title              = Loc.T("tab_documents");
        HeaderLabel.Text   = Loc.T("documents_title");
        BtnAddFile.Content = Loc.T("documents_add_file");
        BtnOpenFolder.Content = Loc.T("documents_open_folder");
        BtnRefresh.Content = "🔄";
        BtnRefresh.ToolTip = Loc.T("documents_refresh_tip");
        BtnAddFile.ToolTip = Loc.T("documents_add_file");
        BtnOpenFolder.ToolTip = Loc.T("documents_open_folder");
        BtnClose.Content   = Loc.T("btn_close");
    }

    private void Render()
    {
        FilesList.Children.Clear();

        if (!Directory.Exists(_docsFolder))
        {
            FilesList.Children.Add(Empty(Loc.T("documents_no_folder")));
            return;
        }

        var files = Directory.GetFiles(_docsFolder)
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        if (files.Count == 0)
        {
            FilesList.Children.Add(Empty(Loc.T("documents_empty")));
            return;
        }

        foreach (var file in files)
            FilesList.Children.Add(BuildFileRow(file));
    }

    private UIElement BuildFileRow(string filePath)
    {
        var name = Path.GetFileName(filePath);
        var ext  = Path.GetExtension(filePath).ToLowerInvariant();
        var size = new FileInfo(filePath).Length;
        var sizeText = size > 1_048_576 ? $"{size / 1_048_576.0:F1} МБ"
                     : size > 1024       ? $"{size / 1024} КБ"
                     :                    $"{size} Б";

        string icon = ext == ".pdf"               ? "📄"
                    : ImageExts.Contains(ext)      ? "🖼"
                    : DicomExts.Contains(ext)      ? "🩻"
                    : ext is ".doc" or ".docx"     ? "📝"
                    :                               "📎";

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconTb = new TextBlock { Text = icon, FontSize = 18, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) };
        Grid.SetColumn(iconTb, 0); grid.Children.Add(iconTb);

        var nameTb = new TextBlock
        {
            Text = name, FontSize = 13,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Cursor = Cursors.Hand
        };
        nameTb.MouseLeftButtonUp += (_, _) => OpenFile(filePath);
        Grid.SetColumn(nameTb, 1); grid.Children.Add(nameTb);

        var sizeTb = new TextBlock
        {
            Text = sizeText, FontSize = 11,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };
        Grid.SetColumn(sizeTb, 2); grid.Children.Add(sizeTb);

        var delBtn = new Button
        {
            Content = "✕", FontSize = 12,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2, 6, 2), Cursor = Cursors.Hand,
            Tag = filePath
        };
        delBtn.Click += DeleteFile_Click;
        Grid.SetColumn(delBtn, 3); grid.Children.Add(delBtn);

        var row = new Border
        {
            Padding = new Thickness(4, 6, 4, 6),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = (Brush)FindResource("SeparatorBrush"),
            Child = grid, Cursor = Cursors.Hand
        };
        row.MouseLeftButtonUp += (_, _) => OpenFile(filePath);
        return row;
    }

    private static void OpenFile(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { }
    }

    private void DeleteFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string path)
        {
            var name = Path.GetFileName(path);
            if (MessageBox.Show(this, Loc.T("documents_delete_confirm", ("name", name)),
                    Loc.T("confirm_title"), MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes) return;
            try { File.Delete(path); Render(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Loc.T("error_title")); }
        }
    }

    private void BtnAddFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = Loc.T("documents_add_file"),
            Filter = "Все файлы (*.*)|*.*|PDF (*.pdf)|*.pdf|Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.webp|DICOM (*.dcm)|*.dcm",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != true) return;

        Directory.CreateDirectory(_docsFolder);
        foreach (var src in dlg.FileNames)
        {
            var dest = Path.Combine(_docsFolder, Path.GetFileName(src));
            if (File.Exists(dest))
            {
                var name = Path.GetFileNameWithoutExtension(src);
                var ext  = Path.GetExtension(src);
                dest = Path.Combine(_docsFolder, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
            }
            try { File.Copy(src, dest); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Loc.T("error_title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        Render();
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_docsFolder);
        try { Process.Start(new ProcessStartInfo(_docsFolder) { UseShellExecute = true }); }
        catch { }
        Render();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => Render();

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private TextBlock Empty(string text) => new()
    {
        Text = text, FontSize = 13,
        Foreground = (Brush)FindResource("TextSecondaryBrush"),
        Margin = new Thickness(8, 16, 8, 8),
        TextWrapping = TextWrapping.Wrap
    };
}
