using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TakingMedications.Common;
using TakingMedications.Services;
using TakingMedications.Views;

namespace TakingMedications;

public partial class MainWindow : Window
{
    private readonly MedAppContext _ctx;
    private TrayService? _tray;

    private static readonly string _appVersion = GetAppVersion();

    private static string GetAppVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version!;
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    public MainWindow()
    {
        _ctx = new MedAppContext(AppPaths.ResolveDataDir());
        ThemeService.SetTheme(_ctx.State.Settings.Theme);
        ThemeService.ApplyBorderPreset(_ctx.State.Settings.BorderColorPreset);

        InitializeComponent();

        // Иконка загружается из файла рядом с exe (не embedded-ресурс)
        var iconFile = System.IO.Path.Combine(PathHelpers.BaseDir, "Medicines.ico");
        if (System.IO.File.Exists(iconFile))
            Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconFile));

        ScheduleViewControl.Initialize(_ctx);
        HistoryViewControl.Initialize(_ctx);
        FinanceViewControl.Initialize(_ctx);
        ForecastViewControl.Initialize(_ctx);

        Loc.LanguageChanged += ApplyLocalization;
        _tray = new TrayService(_ctx, this);

        Closed += (_, _) =>
        {
            Loc.LanguageChanged -= ApplyLocalization;
            _tray?.Dispose();
            System.Windows.Application.Current.Shutdown();
        };

        ApplyLocalization();
        Loaded += async (_, _) => await Updater.CheckForUpdateAsync(Loc.CurrentLang);
    }

    public void RefreshLocalization() => ApplyLocalization();

    private void ApplyLocalization()
    {
        var title = Loc.T("app_title");
        Title = $"{title} v{_appVersion}";
        TitleLabel.Text = $"{title} v{_appVersion}";

        var name = _ctx.State.Settings.PatientName;
        SubLabel.Text = string.IsNullOrEmpty(name)
            ? Loc.T("app_data_folder", ("path", AppPaths.ResolveDataDir()))
            : name;

        TtExportPdf.Content  = Loc.T("btn_export_pdf");
        TtPressure.Content   = Loc.T("btn_pressure");
        TtNotes.Content      = Loc.T("btn_notes");
        TtDocuments.Content  = Loc.T("tab_documents");
        TtSettings.Content   = Loc.T("btn_settings");
        UpdateReminderButton();

        TabSchedule.Header  = Loc.T("tab_schedule");
        TabHistory.Header   = Loc.T("tab_history");
        TabFinance.Header   = Loc.T("tab_finance");
        TabForecast.Header  = Loc.T("tab_forecast");
        TabAskAi.Header     = Loc.T("tab_ask_ai");
    }

    private void UpdateReminderButton()
    {
        bool on = _ctx.State.Settings.RemindersEnabled;
        BtnReminders.Content    = on ? "🔔" : "🔕";
        TtReminders.Content     = Loc.T(on ? "reminder_on" : "reminder_off");
        BtnReminders.Background = on
            ? (Brush)FindResource("SuccessBrush")
            : (Brush)FindResource("DangerBrush");
    }

    private void UpdateLabel_Click(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo(
            "https://github.com/andrey1b/TakingMedications/releases/latest")
        { UseShellExecute = true });
    }

    private void BtnNotes_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new NotesDialog(_ctx) { Owner = this };
        dlg.ShowDialog();
    }

    private void BtnDocuments_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DocumentsDialog(_ctx) { Owner = this };
        dlg.ShowDialog();
    }

    private void BtnPressure_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PressureDialog(_ctx) { Owner = this };
        dlg.ShowDialog();
        _ctx.NotifyChanged();
    }

    private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ExportPdfDialog(_ctx) { Owner = this };
        dlg.ShowDialog();
    }

    private void BtnReminders_Click(object sender, RoutedEventArgs e)
    {
        if (_tray == null) return;
        _tray.RemindersEnabled = !_tray.RemindersEnabled;
        UpdateReminderButton();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_ctx) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            ApplyLocalization();
        }
    }
}
