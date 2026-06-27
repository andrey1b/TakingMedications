// Явный псевдоним для WinForms — не добавляем UseWindowsForms=true,
// чтобы не создавать конфликты имён в WPF-файлах проекта.
using SWF = System.Windows.Forms;

using System;
using System.Linq;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Threading;
using TakingMedications.Common;

namespace TakingMedications.Services;

/// <summary>
/// Управляет иконкой в системном трее, напоминаниями о приёме лекарств
/// и сворачиванием главного окна в трей.
/// Порт Python med_tray.py + med_reminders.py.
/// </summary>
public sealed class TrayService : IDisposable
{
    // Сколько минут после scheduled-времени напоминание ещё актуально
    private const int WindowMin = 5;
    // Пауза между тиками проверки (мс)
    private const int CheckIntervalMs = 30_000;
    // Если между тиками прошло больше (сек) — считаем, что ПК спал
    private const int SleepDetectGap = 120;

    private readonly MedAppContext _ctx;
    private readonly Window        _mainWindow;
    private readonly SWF.NotifyIcon _tray;
    private readonly DispatcherTimer _timer;

    private System.Collections.Generic.HashSet<string> _shownToday = new();
    private DateTime? _lastCheckTime;
    private bool      _firstCheckDone;
    private DateTime  _lastReminderDate;

    public bool RemindersEnabled
    {
        get => _ctx.State.Settings.RemindersEnabled;
        set
        {
            _ctx.State.Settings.RemindersEnabled = value;
            _ctx.SaveState();
        }
    }

    public TrayService(MedAppContext ctx, Window mainWindow)
    {
        _ctx        = ctx;
        _mainWindow = mainWindow;

        // ── Иконка трея ──────────────────────────────────────────────
        var menu = new SWF.ContextMenuStrip();
        var miShow     = new SWF.ToolStripMenuItem(Loc.T("tray_show"),    null, (_, _) => ShowWindow());
        var miSettings = new SWF.ToolStripMenuItem(Loc.T("btn_settings"), null, (_, _) => OpenSettings());
        var miExit     = new SWF.ToolStripMenuItem(Loc.T("tray_exit"),    null, (_, _) => ExitApp());
        menu.Items.AddRange([miShow, new SWF.ToolStripSeparator(), miSettings, new SWF.ToolStripSeparator(), miExit]);

        _tray = new SWF.NotifyIcon
        {
            Icon               = System.Drawing.SystemIcons.Application,
            Visible            = true,
            Text               = Loc.T("app_title"),
            ContextMenuStrip   = menu,
        };
        _tray.DoubleClick += (_, _) => ShowWindow();

        // Обновлять подписи при смене языка
        Loc.LanguageChanged += () =>
        {
            _tray.Text    = Loc.T("app_title");
            miShow.Text     = Loc.T("tray_show");
            miSettings.Text = Loc.T("btn_settings");
            miExit.Text     = Loc.T("tray_exit");
        };

        // ── Сворачивание в трей при нажатии крестика ─────────────────
        _mainWindow.Closing += OnWindowClosing;

        // ── Таймер напоминаний ────────────────────────────────────────
        LoadShownFromState();

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(CheckIntervalMs),
        };
        _timer.Tick += (_, _) => CheckReminders();
        _timer.Start();

        // Первая проверка при старте (ищем пропущенные за сегодня)
        CheckReminders();
    }

    // ────────────────────────────────────────────────────────────────
    //  Окно
    // ────────────────────────────────────────────────────────────────

    private void ShowWindow()
    {
        _mainWindow.Dispatcher.Invoke(() =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        });
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var mode = _ctx.State.Settings.BackgroundMode ?? "none";
        if (mode == "none")
        {
            // В режиме "только в окне" закрытие = выход из приложения.
            // Не отменяем событие — WPF завершит окно и приложение.
            return;
        }
        // Все остальные режимы — сворачиваем в трей.
        e.Cancel = true;
        _mainWindow.Hide();
        _tray.ShowBalloonTip(3000, Loc.T("app_title"),
            Loc.T("tray_hidden_hint", ("title", Loc.T("app_title"))),
            SWF.ToolTipIcon.Info);
    }

    private void OpenSettings()
    {
        ShowWindow();
        _mainWindow.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var dlg = new Views.SettingsWindow(_ctx) { Owner = _mainWindow };
            if (dlg.ShowDialog() == true)
                (_mainWindow as MainWindow)?.RefreshLocalization();
        }));
    }

    private static void ExitApp()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(
            () => System.Windows.Application.Current.Shutdown());
    }

    // ────────────────────────────────────────────────────────────────
    //  Напоминания
    // ────────────────────────────────────────────────────────────────

    private void LoadShownFromState()
    {
        var extra = _ctx.State.RawExtras;
        if (extra.TryGetValue("_reminders_shown", out var token) &&
            token is Newtonsoft.Json.Linq.JObject obj)
        {
            var savedDate = obj["date"]?.ToString();
            var today     = DateTime.Now.ToString("yyyy-MM-dd");
            if (savedDate == today)
            {
                var times = obj["times"] as Newtonsoft.Json.Linq.JArray;
                if (times != null)
                    _shownToday = new System.Collections.Generic.HashSet<string>(
                        times.Select(t => t.ToString()));
                _lastReminderDate = DateTime.Now.Date;
                return;
            }
        }
        _shownToday = new();
        _lastReminderDate = DateTime.MinValue.Date;
    }

    private void PersistShown()
    {
        _ctx.State.RawExtras["_reminders_shown"] = Newtonsoft.Json.Linq.JToken.FromObject(new
        {
            date  = DateTime.Now.ToString("yyyy-MM-dd"),
            times = _shownToday.OrderBy(t => t).ToArray(),
        });
        _ctx.SaveState();
    }

    private void CheckReminders()
    {
        if (!RemindersEnabled)
        {
            _lastCheckTime  = DateTime.Now;
            _firstCheckDone = true;
            return;
        }

        var now   = DateTime.Now;
        var today = now.Date;

        // Смена суток — сброс
        if (_lastReminderDate != today)
        {
            _shownToday.Clear();
            _lastReminderDate = today;
            _ctx.State.RawExtras.Remove("_reminders_shown");
            _ctx.SaveState();
        }

        // Нужен ли скан пропущенных?
        bool scanMissed = false;
        if (!_firstCheckDone)
        {
            scanMissed      = true;
            _firstCheckDone = true;
        }
        else if (_lastCheckTime.HasValue)
        {
            var gap = (now - _lastCheckTime.Value).TotalSeconds;
            if (gap > SleepDetectGap) scanMissed = true;
        }
        _lastCheckTime = now;

        int currentMin = now.Hour * 60 + now.Minute;
        var missed = new System.Collections.Generic.List<(string time, System.Collections.Generic.List<string> names)>();
        bool newlyShown = false;

        // Строим расписание: время → список имён препаратов
        var schedule = _ctx.Sections
            .SelectMany(s => s.Items)
            .Where(m => !string.IsNullOrEmpty(m.Time))
            .GroupBy(m => m.Time!)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Name).ToList());

        foreach (var (timeStr, names) in schedule.OrderBy(kv => kv.Key))
        {
            if (_shownToday.Contains(timeStr)) continue;

            var triggerMin = ParseHhMm(timeStr);
            if (triggerMin is null) continue;

            if (triggerMin <= currentMin && currentMin <= triggerMin + WindowMin)
            {
                ShowToast(timeStr, names);
                _shownToday.Add(timeStr);
                newlyShown = true;
            }
            else if (scanMissed && currentMin > triggerMin + WindowMin)
            {
                missed.Add((timeStr, names));
            }
        }

        if (missed.Count > 0)
        {
            ShowMissed(missed);
            foreach (var (t, _) in missed) _shownToday.Add(t);
            newlyShown = true;
        }

        if (newlyShown) PersistShown();
    }

    private void ShowToast(string timeStr, System.Collections.Generic.List<string> names)
    {
        var title = Loc.T("reminder_toast_title", ("time", timeStr));
        var msg   = Loc.T("reminder_toast_msg",
            ("names", string.Join("\n", names.Select(n => $"• {n}"))));
        _tray.ShowBalloonTip(60_000, title, msg, SWF.ToolTipIcon.Info);

        // Голосовое напоминание
        if (_ctx.State.Settings.VoiceRemindersEnabled)
            SpeakNamesAsync(names, _ctx.State.Settings.VoiceId);

        // Telegram
        if (_ctx.State.Settings.TelegramEnabled)
            SendTelegramAsync(title, msg);
    }

    private static void SpeakNamesAsync(
        System.Collections.Generic.List<string> names, string? voiceId)
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var synth = new SpeechSynthesizer();
                if (!string.IsNullOrEmpty(voiceId))
                    synth.SelectVoice(voiceId);
                foreach (var name in names)
                    synth.Speak(name);
            }
            catch { }
        });
    }

    private void SendTelegramAsync(string title, string msg)
    {
        var token = _ctx.State.Settings.TelegramBotToken;
        var chatIdStr = _ctx.State.Settings.TelegramChatId;
        if (string.IsNullOrEmpty(token) || !long.TryParse(chatIdStr, out var chatId)) return;
        var text = $"🔔 {title}\n{msg}";
        _ = TelegramService.SendMessageAsync(token, chatId, text);
    }

    private void ShowMissed(System.Collections.Generic.List<(string time, System.Collections.Generic.List<string> names)> missed)
    {
        var lines = missed.Select(m => $"• {m.time} — {string.Join(", ", m.names)}");
        var title = Loc.T("reminder_missed_title");
        var msg   = Loc.T("reminder_missed_msg", ("lines", string.Join("\n", lines)));
        _tray.ShowBalloonTip(120_000, title, msg, SWF.ToolTipIcon.Warning);
    }

    private static int? ParseHhMm(string s)
    {
        var parts = s.Split(':');
        if (parts.Length != 2) return null;
        if (!int.TryParse(parts[0], out var h)) return null;
        if (!int.TryParse(parts[1], out var m)) return null;
        return h * 60 + m;
    }

    public void Dispose()
    {
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _mainWindow.Closing -= OnWindowClosing;
    }
}
