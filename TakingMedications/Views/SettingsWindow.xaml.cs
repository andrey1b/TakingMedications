using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using TakingMedications.Common;
using TakingMedications.Services;

namespace TakingMedications.Views;

public partial class SettingsWindow : Window
{
    private readonly MedAppContext _ctx;
    private readonly string _initialLang;
    private readonly string _initialTheme;
    private CancellationTokenSource? _tgCts;

    public SettingsWindow(MedAppContext ctx)
    {
        InitializeComponent();
        _ctx = ctx;
        _initialLang  = Loc.CurrentLang;
        _initialTheme = ctx.State.Settings.Theme;

        SelectLangRadio(Loc.CurrentLang);
        SelectThemeRadio(ctx.State.Settings.Theme);

        PatientBox.Text = ctx.State.Settings.PatientName ?? "";
        DobBox.Text     = ctx.State.Settings.PatientDob  ?? "";
        var gender = ctx.State.Settings.PatientGender ?? "male";
        RbMale.IsChecked   = gender != "female";
        RbFemale.IsChecked = gender == "female";
        SelectModeRadio(ctx.State.Settings.BackgroundMode ?? "none");

        // Напоминания
        ChkRemindersVisual.IsChecked = ctx.State.Settings.RemindersEnabled;
        ChkRemindersVoice.IsChecked  = ctx.State.Settings.VoiceRemindersEnabled;
        LoadVoices(ctx.State.Settings.VoiceId);
        UpdateVoicePanelState();
        ChkRemindersVoice.Checked   += (_, _) => UpdateVoicePanelState();
        ChkRemindersVoice.Unchecked += (_, _) => UpdateVoicePanelState();

        // Telegram — читаем из telegram.json (Python-совместимый файл)
        // и синхронизируем с _settings если нужно
        SyncTelegramFromFile(ctx);
        TgTokenBox.Text = ctx.State.Settings.TelegramBotToken ?? "";
        ChkTgEnable.IsChecked = ctx.State.Settings.TelegramEnabled;
        UpdateTgStatus();

        // Внешний вид
        SelectBorderRadio(ctx.State.Settings.BorderColorPreset ?? "medium");

        ApplyLocalization();
        Loc.LanguageChanged += ApplyLocalization;
        Closed += (_, _) =>
        {
            Loc.LanguageChanged -= ApplyLocalization;
            _tgCts?.Cancel();
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private void SelectLangRadio(string lang)
    {
        foreach (var rb in new[] { RbRu, RbUk, RbEn, RbEs, RbDe, RbFr, RbIt })
            if (rb.Tag as string == lang) { rb.IsChecked = true; return; }
        RbRu.IsChecked = true;
    }

    private void SelectThemeRadio(string theme)
    {
        RbThemeDark.IsChecked  = theme != ThemeService.Light;
        RbThemeLight.IsChecked = theme == ThemeService.Light;
    }

    private void SelectModeRadio(string mode)
    {
        foreach (var rb in new[] { RbModeNone, RbModeTray, RbModeAutostart, RbModeHidden })
            if (rb.Tag as string == mode) { rb.IsChecked = true; return; }
        RbModeNone.IsChecked = true;
    }

    private void SelectBorderRadio(string preset)
    {
        RbBorderLight.IsChecked  = preset == "light";
        RbBorderMedium.IsChecked = preset == "medium" || (preset != "light" && preset != "dark");
        RbBorderDark.IsChecked   = preset == "dark";
    }

    private string GetSelectedLang()
    {
        foreach (var rb in new[] { RbRu, RbUk, RbEn, RbEs, RbDe, RbFr, RbIt })
            if (rb.IsChecked == true) return rb.Tag as string ?? "ru";
        return "ru";
    }

    private string GetSelectedTheme()
        => RbThemeLight.IsChecked == true ? ThemeService.Light : ThemeService.Dark;

    private string GetSelectedMode()
    {
        foreach (var rb in new[] { RbModeNone, RbModeTray, RbModeAutostart, RbModeHidden })
            if (rb.IsChecked == true) return rb.Tag as string ?? "none";
        return "none";
    }

    private string GetSelectedBorderPreset()
    {
        if (RbBorderLight.IsChecked == true) return "light";
        if (RbBorderDark.IsChecked == true)  return "dark";
        return "medium";
    }

    // ── Голос ────────────────────────────────────────────────────────

    private void LoadVoices(string? currentId)
    {
        CbVoice.Items.Clear();
        CbVoice.Items.Add(new ComboBoxItem
        {
            Content = Loc.T("settings_voice_auto"),
            Tag     = ""
        });

        try
        {
            using var synth = new SpeechSynthesizer();
            foreach (var v in synth.GetInstalledVoices().Where(v => v.Enabled))
            {
                var info = v.VoiceInfo;
                // Звёздочка для голосов с кириллицей (обычно RHVoice / TTS-движки)
                bool isCyrillic = info.Culture.TwoLetterISOLanguageName is "ru" or "uk";
                var label = isCyrillic ? $"★ {info.Name}" : info.Name;
                CbVoice.Items.Add(new ComboBoxItem { Content = label, Tag = info.Name });
            }
        }
        catch { /* SAPI недоступен */ }

        // Выбрать текущий
        int idx = 0;
        if (!string.IsNullOrEmpty(currentId))
        {
            for (int i = 1; i < CbVoice.Items.Count; i++)
            {
                if ((CbVoice.Items[i] as ComboBoxItem)?.Tag as string == currentId)
                { idx = i; break; }
            }
        }
        CbVoice.SelectedIndex = idx;
    }

    private void UpdateVoicePanelState()
    {
        VoicePanel.IsEnabled = ChkRemindersVoice.IsChecked == true;
        VoicePanel.Opacity   = VoicePanel.IsEnabled ? 1.0 : 0.5;
    }

    // ── Telegram ─────────────────────────────────────────────────────

    private void UpdateTgStatus()
    {
        var chatId = _ctx.State.Settings.TelegramChatId;
        bool connected = !string.IsNullOrEmpty(chatId) &&
                         !string.IsNullOrEmpty(_ctx.State.Settings.TelegramBotToken);

        LblTgStatus.Text = connected
            ? Loc.T("settings_tg_connected") + Loc.T("settings_tg_id_label") + chatId
            : Loc.T("settings_tg_not_connected");

        BtnTgDisconnect.IsEnabled = connected;
        BtnTgTest.IsEnabled       = connected;
    }

    // ── Локализация ───────────────────────────────────────────────────

    private void ApplyLocalization()
    {
        Title            = Loc.T("settings_title");
        HeaderLabel.Text = Loc.T("settings_title");

        LblLangSection.Text = Loc.T("settings_language");
        LangNote.Text       = Loc.T("settings_lang_note");

        LblThemeSection.Text    = Loc.T("settings_theme_section");
        RbThemeDark.Content     = Loc.T("settings_theme_dark");
        RbThemeLight.Content    = Loc.T("settings_theme_light");

        LblPatientSection.Text = Loc.T("settings_patient_section");
        LblFio.Text            = Loc.T("settings_patient_fio");
        LblDob.Text            = Loc.T("settings_patient_dob");
        LblGender.Text         = Loc.T("settings_patient_gender");
        RbMale.Content         = Loc.T("settings_patient_male");
        RbFemale.Content       = Loc.T("settings_patient_female");
        BtnManagePatients.Content = Loc.T("settings_manage_patients");

        LblAppModeSection.Text    = Loc.T("settings_app_mode_section");
        RbModeNone.Content        = Loc.T("settings_mode_window");
        LblModeWindowDesc.Text    = Loc.T("settings_mode_window_desc");
        RbModeTray.Content        = Loc.T("settings_mode_tray");
        LblModeTrayDesc.Text      = Loc.T("settings_mode_tray_desc");
        RbModeAutostart.Content   = Loc.T("settings_mode_autostart");
        LblModeAutostartDesc.Text = Loc.T("settings_mode_autostart_desc");
        RbModeHidden.Content      = Loc.T("settings_mode_hidden");
        LblModeHiddenDesc.Text    = Loc.T("settings_mode_hidden_desc");

        // Напоминания
        LblRemindersSection.Text      = Loc.T("settings_reminders_section");
        ChkRemindersVisual.Content    = Loc.T("settings_reminders_visual");
        LblRemindersVisualDesc.Text   = Loc.T("settings_reminders_visual_desc");
        ChkRemindersVoice.Content     = Loc.T("settings_reminders_voice");
        LblRemindersVoiceDesc.Text    = Loc.T("settings_reminders_voice_desc");
        LblVoiceLabel.Text            = Loc.T("settings_voice_label");
        BtnVoiceTest.Content          = Loc.T("settings_voice_test");

        // Telegram
        LblTgSection.Text    = Loc.T("settings_tg_section");
        ChkTgEnable.Content  = Loc.T("settings_tg_enable");
        LblTgEnableDesc.Text = Loc.T("settings_tg_enable_desc");
        LblTgTokenLabel.Text = Loc.T("settings_tg_token_label");
        BtnTgHow.Content     = Loc.T("settings_tg_how");
        BtnTgConnect.Content    = Loc.T("settings_tg_connect");
        BtnTgDisconnect.Content = Loc.T("settings_tg_disconnect");
        BtnTgTest.Content       = Loc.T("settings_tg_test");
        UpdateTgStatus();

        // Внешний вид
        LblAppearanceSection.Text = Loc.T("settings_appearance_section");
        LblBorderLabel.Text       = Loc.T("settings_border_label");
        RbBorderLight.Content     = Loc.T("settings_border_light");
        RbBorderMedium.Content    = Loc.T("settings_border_medium");
        RbBorderDark.Content      = Loc.T("settings_border_dark");
        LblBorderDesc.Text        = Loc.T("settings_border_desc");

        BtnSave.Content   = Loc.T("btn_save");
        BtnCancel.Content = Loc.T("btn_cancel");
    }

    // ── Event handlers ────────────────────────────────────────────────

    private void LangRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string lang)
            Loc.SetLang(lang);
    }

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string theme)
            ThemeService.SetTheme(theme);
    }

    private void BtnManagePatients_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(Loc.T("settings_feature_wip"), Loc.T("warning_title"),
                        MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnVoiceTest_Click(object sender, RoutedEventArgs e)
    {
        var voiceId = (CbVoice.SelectedItem as ComboBoxItem)?.Tag as string;
        var text    = Loc.T("settings_voice_test_text");
        SpeakAsync(text, string.IsNullOrEmpty(voiceId) ? null : voiceId);
    }

    private static void SpeakAsync(string text, string? voiceId)
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var synth = new SpeechSynthesizer();
                if (!string.IsNullOrEmpty(voiceId))
                    synth.SelectVoice(voiceId);
                synth.Speak(text);
            }
            catch { }
        });
    }

    private void BtnTgHow_Click(object sender, RoutedEventArgs e)
    {
        var url = Loc.T("settings_tg_how_url");
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private async void BtnTgConnect_Click(object sender, RoutedEventArgs e)
    {
        var token = TgTokenBox.Text.Trim();
        if (string.IsNullOrEmpty(token))
        {
            MessageBox.Show(Loc.T("settings_tg_token_invalid"), Loc.T("warning_title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnTgConnect.IsEnabled = false;
        var botName = await TelegramService.ValidateTokenAsync(token);
        if (botName == null)
        {
            BtnTgConnect.IsEnabled = true;
            MessageBox.Show(Loc.T("settings_tg_token_invalid"), Loc.T("warning_title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(Loc.T("settings_tg_connecting", ("bot", botName)),
            Loc.T("settings_tg_section"), MessageBoxButton.OK, MessageBoxImage.Information);

        _tgCts?.Cancel();
        _tgCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var chatId = await TelegramService.WaitForFirstMessageAsync(token, _tgCts.Token);

        BtnTgConnect.IsEnabled = true;

        if (chatId == null)
        {
            MessageBox.Show(Loc.T("settings_tg_connect_fail"), Loc.T("warning_title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _ctx.State.Settings.TelegramBotToken = token;
        _ctx.State.Settings.TelegramChatId   = chatId.ToString();
        _ctx.State.Settings.TelegramEnabled  = true;
        ChkTgEnable.IsChecked = true;
        SaveTelegramToFile(token, chatId.ToString());
        _ctx.SaveState();
        UpdateTgStatus();

        MessageBox.Show(Loc.T("settings_tg_connect_ok", ("id", chatId)),
            Loc.T("settings_tg_section"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnTgDisconnect_Click(object sender, RoutedEventArgs e)
    {
        _ctx.State.Settings.TelegramEnabled  = false;
        _ctx.State.Settings.TelegramChatId   = "";
        ChkTgEnable.IsChecked = false;
        _ctx.SaveState();
        UpdateTgStatus();
    }

    private async void BtnTgTest_Click(object sender, RoutedEventArgs e)
    {
        var token  = _ctx.State.Settings.TelegramBotToken;
        var chatId = _ctx.State.Settings.TelegramChatId;
        if (string.IsNullOrEmpty(token) || !long.TryParse(chatId, out var id)) return;

        BtnTgTest.IsEnabled = false;
        var ok = await TelegramService.SendMessageAsync(token, id, "Привет! 👋");
        BtnTgTest.IsEnabled = true;

        MessageBox.Show(ok ? Loc.T("settings_tg_test_ok") : Loc.T("settings_tg_test_fail"),
            Loc.T("settings_tg_section"), MessageBoxButton.OK,
            ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var s = _ctx.State.Settings;

        s.Language       = GetSelectedLang();
        s.Theme          = GetSelectedTheme();
        s.BackgroundMode = GetSelectedMode();
        s.BorderColorPreset = GetSelectedBorderPreset();

        var name   = PatientBox.Text.Trim();
        var dob    = DobBox.Text.Trim();
        var gender = RbFemale.IsChecked == true ? "female" : "male";
        s.PatientName   = string.IsNullOrEmpty(name) ? null : name;
        s.PatientDob    = string.IsNullOrEmpty(dob)  ? null : dob;
        s.PatientGender = gender;

        s.RemindersEnabled      = ChkRemindersVisual.IsChecked == true;
        s.VoiceRemindersEnabled = ChkRemindersVoice.IsChecked == true;
        s.VoiceId = (CbVoice.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

        s.TelegramEnabled   = ChkTgEnable.IsChecked == true;
        s.TelegramBotToken  = TgTokenBox.Text.Trim();
        SaveTelegramToFile(s.TelegramBotToken, s.TelegramChatId ?? "");

        // Применяем автозапуск Windows
        if (s.BackgroundMode == "start_with_windows")
            AutostartHelper.Register();
        else
            AutostartHelper.Unregister();

        // Python-совместимый блок _patient
        if (_ctx.State.RawExtras.TryGetValue("_patient", out var ptToken)
            && ptToken is Newtonsoft.Json.Linq.JObject ptObj)
        {
            if (!string.IsNullOrEmpty(name)) ptObj["full_name"]  = name;
            if (!string.IsNullOrEmpty(dob))  ptObj["birth_date"] = dob;
            ptObj["gender"] = gender;
        }

        // Применить пресет границ
        ThemeService.ApplyBorderPreset(s.BorderColorPreset);

        _ctx.SaveState();
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (Loc.CurrentLang != _initialLang)
            Loc.SetLang(_initialLang);
        if (ThemeService.Current != _initialTheme)
            ThemeService.SetTheme(_initialTheme);
        DialogResult = false;
        Close();
    }

    // ── telegram.json совместимость с Python ─────────────────────────

    private static string TelegramFilePath()
        => Path.Combine(AppPaths.ResolveDataDir(), "telegram.json");

    /// <summary>
    /// Читает telegram.json (Python-формат) и, если там есть токен,
    /// которого нет в _settings — копирует в _settings.
    /// </summary>
    private static void SyncTelegramFromFile(MedAppContext ctx)
    {
        try
        {
            var path = TelegramFilePath();
            if (!File.Exists(path)) return;

            var obj = JObject.Parse(File.ReadAllText(path));
            var fileToken  = obj["bot_token"]?.ToString() ?? "";
            var fileChatId = obj["chat_id"]?.ToString()   ?? "";

            if (string.IsNullOrEmpty(ctx.State.Settings.TelegramBotToken) && !string.IsNullOrEmpty(fileToken))
            {
                ctx.State.Settings.TelegramBotToken = fileToken;
                ctx.State.Settings.TelegramEnabled  = true;
            }
            if (string.IsNullOrEmpty(ctx.State.Settings.TelegramChatId) && !string.IsNullOrEmpty(fileChatId))
                ctx.State.Settings.TelegramChatId = fileChatId;
        }
        catch { }
    }

    /// <summary>
    /// Сохраняет токен и chat_id обратно в telegram.json,
    /// чтобы Python тоже видел актуальные данные.
    /// </summary>
    private static void SaveTelegramToFile(string token, string chatId)
    {
        try
        {
            var path = TelegramFilePath();
            var obj = new JObject { ["bot_token"] = token };
            if (!string.IsNullOrEmpty(chatId))
                obj["chat_id"] = chatId;
            File.WriteAllText(path, obj.ToString(Newtonsoft.Json.Formatting.Indented));
        }
        catch { }
    }
}
