using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using TakingMedications.Common;
using TakingMedications.Models;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class ExportPdfDialog : Window
{
    private readonly MedAppContext _ctx;

    public ExportPdfDialog(MedAppContext ctx)
    {
        InitializeComponent();
        _ctx = ctx;

        DateFrom.SelectedDate = DateTime.Today.AddDays(-29);
        DateTo.SelectedDate   = DateTime.Today;
        UpdatePathFromPeriod();

        DateFrom.SelectedDateChanged += (_, _) => UpdatePathFromPeriod();
        DateTo.SelectedDateChanged   += (_, _) => UpdatePathFromPeriod();

        ApplyLocalization();
        Loc.LanguageChanged += ApplyLocalization;
        Closed += (_, _) => Loc.LanguageChanged -= ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        Title = Loc.T("export_title");
        HeaderLabel.Text = Loc.T("export_title");
        LblPeriod.Text   = Loc.T("export_period");
        LblFrom.Text     = Loc.T("export_from");
        LblTo.Text       = Loc.T("export_to");
        LblInclude.Text  = Loc.T("export_include");
        LblFile.Text     = Loc.T("export_path");
        IncSchedule.Content = Loc.T("export_inc_schedule");
        IncHistory.Content  = Loc.T("export_inc_history");
        IncPressure.Content = Loc.T("export_inc_pressure");
        IncFinance.Content  = Loc.T("export_inc_finance");
        OpenAfter.Content   = Loc.T("export_open_after");
        BtnBrowse.Content   = Loc.T("export_browse");
        BtnSave.Content     = Loc.T("export_btn_save");
        BtnCancel.Content   = Loc.T("btn_cancel");
        QuickBtn7.Content   = Loc.T("export_quick_7");
        QuickBtn30.Content  = Loc.T("export_quick_30");
        QuickBtn90.Content  = Loc.T("export_quick_90");
        QuickBtnYr.Content  = Loc.T("export_quick_year");
    }

    private void UpdatePathFromPeriod()
    {
        var from = DateFrom.SelectedDate ?? DateTime.Today.AddDays(-29);
        var to   = DateTo.SelectedDate   ?? DateTime.Today;
        var fname = Loc.T("export_default_name",
            ("from", from.ToString("yyyy-MM-dd")),
            ("to",   to.ToString("yyyy-MM-dd")));
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        PathBox.Text = Path.Combine(dir, fname);
    }

    private void Quick7_Click(object sender, RoutedEventArgs e)  => SetPeriodLastN(7);
    private void Quick30_Click(object sender, RoutedEventArgs e) => SetPeriodLastN(30);
    private void Quick90_Click(object sender, RoutedEventArgs e) => SetPeriodLastN(90);
    private void QuickYr_Click(object sender, RoutedEventArgs e) => SetPeriodLastN(365);

    private void SetPeriodLastN(int days)
    {
        DateTo.SelectedDate   = DateTime.Today;
        DateFrom.SelectedDate = DateTime.Today.AddDays(-(days - 1));
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf|All files (*.*)|*.*",
            DefaultExt = ".pdf",
            FileName = Path.GetFileName(PathBox.Text),
            InitialDirectory = Path.GetDirectoryName(PathBox.Text)
                               ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dlg.ShowDialog(this) == true) PathBox.Text = dlg.FileName;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var opts = new ReportOptions
        {
            From = DateFrom.SelectedDate ?? DateTime.Today.AddDays(-29),
            To   = DateTo.SelectedDate   ?? DateTime.Today,
            IncludeSchedule = IncSchedule.IsChecked == true,
            IncludeHistory  = IncHistory.IsChecked  == true,
            IncludePressure = IncPressure.IsChecked == true,
            IncludeFinance  = IncFinance.IsChecked  == true,
            PatientName     = _ctx.State.Settings.PatientName,
        };
        if (opts.From > opts.To) (opts.From, opts.To) = (opts.To, opts.From);

        var path = PathBox.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show(this, Loc.T("export_error", ("err", Loc.T("export_path"))),
                Loc.T("error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            PdfReport.Generate(_ctx, opts, path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                Loc.T("export_error", ("err", ex.Message)),
                Loc.T("error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (OpenAfter.IsChecked == true)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                // Если ассоциации нет — просто покажем итог.
                MessageBox.Show(this,
                    Loc.T("export_done", ("path", path)),
                    Loc.T("app_title"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
