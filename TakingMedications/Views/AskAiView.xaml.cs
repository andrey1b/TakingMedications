using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TakingMedications.Common;
using TakingMedications.Services;
using WMedia = System.Windows.Media;

namespace TakingMedications.Views;

// Вкладка «Спросить у ИИ» — единый узнаваемый элемент SeniorHub (перенос из MenuApp/HomeAccounting).
public partial class AskAiView : UserControl
{
    private static readonly (string Name, string Url, string ApiId)[] AiList =
    {
        ("ChatGPT",    "https://chat.openai.com",         ""),
        ("Claude",     "https://claude.ai",                "claude"),
        ("Gemini",     "https://gemini.google.com",        "gemini"),
        ("Copilot",    "https://copilot.microsoft.com",    ""),
        ("Perplexity", "https://www.perplexity.ai",        "perplexity"),
        ("DeepSeek",   "https://chat.deepseek.com",        "deepseek"),
    };

    private static readonly (byte r, byte g, byte b)[] AiColors =
    {
        (16,  163, 127), (190,  90,  40), (66,  133, 244),
        (0,   120, 212), (20,  100, 180), (50,   80, 200),
    };

    private readonly ObservableCollection<AiRow> aiRows = new();

    private string _claudeApiKey = "", _geminiApiKey = "", _deepSeekApiKey = "", _perplexityApiKey = "";

    private static readonly WMedia.Brush BrushError = Frozen(211, 47, 47);   // читаемый и на светлой, и на тёмной теме
    private static readonly WMedia.Brush BrushLight = Frozen(230, 230, 230); // для тёмного диалога ключей

    // Цвет текста ответа берём из активной темы (иначе на светлой теме ответ был бы не виден)
    private WMedia.Brush ThemeText => TryFindResource("TextPrimaryBrush") as WMedia.Brush ?? Frozen(40, 40, 40);

    private static WMedia.Brush Frozen(byte r, byte g, byte b)
    {
        var br = new WMedia.SolidColorBrush(WMedia.Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    private static string AiDataDir => AppPaths.ResolveDataDir();

    public AskAiView()
    {
        InitializeComponent();

        for (int i = 0; i < AiList.Length; i++)
        {
            var (name, url, apiId) = AiList[i];
            aiRows.Add(new AiRow
            {
                Name = name, Url = url, ApiId = apiId,
                HeaderBrush = Frozen(AiColors[i].r, AiColors[i].g, AiColors[i].b),
                StatsFormat = (c, w) => Loc.T("ai_stats", ("chars", c.ToString("N0")), ("words", w.ToString("N0")))
            });
        }
        icAiRows.ItemsSource = aiRows;

        btnAiAsk.Click     += async (_, _) => await AskAllAisAsync();
        btnAiSaveAll.Click += (_, _) => SaveAllResponses();
        btnAiClear.Click   += (_, _) => ClearAllResponses();
        btnAiApiKeys.Click += (_, _) => ShowApiKeyDialog();
        txAiQuestion.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; await AskAllAisAsync(); }
        };

        // Быстрые вопросы по теме приёма лекарств (только подставляют текст, не отправляют)
        btnAiQuick1.Click += (_, _) => txAiQuestion.Text = Loc.T("ai_q_compat");
        btnAiQuick2.Click += (_, _) => txAiQuestion.Text = Loc.T("ai_q_side");
        btnAiQuick3.Click += (_, _) => txAiQuestion.Text = Loc.T("ai_q_time");

        LoadAiSettings();
        ApplyLoc();
        Loc.LanguageChanged += ApplyLoc;
        Unloaded += (_, _) => Loc.LanguageChanged -= ApplyLoc;
    }

    private void ApplyLoc()
    {
        TbAiQuestionLbl.Text   = Loc.T("ai_question");
        btnAiAsk.Content       = Loc.T("ai_ask");
        btnAiSaveAll.Content   = Loc.T("ai_save_all");
        btnAiClear.Content     = Loc.T("ai_clear");
        btnAiApiKeys.Content   = Loc.T("ai_api_keys");
        TbAiQuickLbl.Text      = Loc.T("ai_quick");
        btnAiQuick1.Content    = Loc.T("ai_qb_compat");
        btnAiQuick2.Content    = Loc.T("ai_qb_side");
        btnAiQuick3.Content    = Loc.T("ai_qb_time");
        foreach (var r in aiRows)
        {
            r.SaveLabel = "💾 " + Loc.T("ai_save");
            r.CopyLabel = "📋 " + Loc.T("ai_copy");
        }
    }

    private void AiOpenButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AiRow r)
            Process.Start(new ProcessStartInfo { FileName = r.Url, UseShellExecute = true });
    }

    private void AiSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AiRow r)
            SaveSingleResponse(r);
    }

    private void AiCopyButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AiRow r) return;
        string txt = r.Response.Trim();
        if (string.IsNullOrEmpty(txt))
        {
            MessageBox.Show(Loc.T("ai_nothing_msg"), Loc.T("ai_empty_title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Clipboard.SetText(txt);
        lblAiStatus.Text = "📋 " + Loc.T("ai_copied") + " (" + r.Name + ")";
    }

    private void ClearAllResponses()
    {
        foreach (var r in aiRows) { r.Response = ""; r.ResponseBrush = ThemeText; }
        lblAiStatus.Text = "";
    }

    private async Task AskAllAisAsync()
    {
        string question = txAiQuestion.Text.Trim();
        if (string.IsNullOrEmpty(question))
        {
            MessageBox.Show(Loc.T("ai_empty_msg"), Loc.T("ai_empty_title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(question);

        var tasks         = new List<Task>();
        var browserOpened = new List<string>();

        for (int i = 0; i < aiRows.Count; i++)
        {
            var r = aiRows[i];
            if (!r.Enabled) continue;

            int     idx    = i;
            string? apiKey = ApiKeyFor(r.ApiId);
            bool    hasKey = !string.IsNullOrEmpty(apiKey);

            if (r.ApiId == "claude" && hasKey)
            {
                r.ResponseBrush = ThemeText; r.Response = Loc.T("ai_req_to", ("name", r.Name));
                tasks.Add(AskClaudeAsync(idx, question, apiKey!));
            }
            else if (r.ApiId == "gemini" && hasKey)
            {
                r.ResponseBrush = ThemeText; r.Response = Loc.T("ai_req_to", ("name", r.Name));
                tasks.Add(AskGeminiAsync(idx, question, apiKey!));
            }
            else if (r.ApiId == "deepseek" && hasKey)
            {
                r.ResponseBrush = ThemeText; r.Response = Loc.T("ai_req_to", ("name", r.Name));
                tasks.Add(AskDeepSeekAsync(idx, question, apiKey!));
            }
            else if (r.ApiId == "perplexity" && hasKey)
            {
                r.ResponseBrush = ThemeText; r.Response = Loc.T("ai_req_to", ("name", r.Name));
                tasks.Add(AskPerplexityAsync(idx, question, apiKey!));
            }
            else
            {
                string openUrl = BuildBrowserUrl(r.Name, r.Url, question);
                Process.Start(new ProcessStartInfo { FileName = openUrl, UseShellExecute = true });
                r.ResponseBrush = ThemeText;
                r.Response = Loc.T("ai_browser_note");
                browserOpened.Add(r.Name);
            }
        }

        if (tasks.Count > 0)
        {
            lblAiStatus.Text = Loc.T("ai_status_wait");
            await Task.WhenAll(tasks);
            lblAiStatus.Text = Loc.T("ai_status_done");
        }
        else
        {
            lblAiStatus.Text = browserOpened.Count > 0
                ? Loc.T("ai_status_browser", ("list", string.Join(", ", browserOpened)))
                : Loc.T("ai_status_none");
        }
    }

    private string? ApiKeyFor(string apiId) => apiId switch
    {
        "claude"     => string.IsNullOrEmpty(_claudeApiKey)     ? null : _claudeApiKey,
        "gemini"     => string.IsNullOrEmpty(_geminiApiKey)     ? null : _geminiApiKey,
        "deepseek"   => string.IsNullOrEmpty(_deepSeekApiKey)   ? null : _deepSeekApiKey,
        "perplexity" => string.IsNullOrEmpty(_perplexityApiKey) ? null : _perplexityApiKey,
        _            => null
    };

    private static string BuildBrowserUrl(string name, string url, string question)
    {
        string q = Uri.EscapeDataString(question);
        return name switch
        {
            "Perplexity" => $"https://www.perplexity.ai/search?q={q}",
            "Copilot"    => $"https://www.bing.com/search?q={q}&showconv=1",
            _            => url
        };
    }

    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

    private async Task AskClaudeAsync(int idx, string question, string key)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            req.Headers.Add("x-api-key", key);
            req.Headers.Add("anthropic-version", "2023-06-01");
            string body = JsonSerializer.Serialize(new
            {
                model      = "claude-sonnet-4-6",
                max_tokens = 1024,
                messages   = new[] { new { role = "user", content = question } }
            });
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var resp = await _httpClient.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            { SetResponse(idx, $"{Loc.T("ai_err")} Claude ({(int)resp.StatusCode}): {json}", BrushError); return; }

            using var doc = JsonDocument.Parse(json);
            string text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            SetResponse(idx, text, null);
        }
        catch (Exception ex) { SetResponse(idx, $"{Loc.T("ai_err")}: {ex.Message}", BrushError); }
    }

    private async Task AskGeminiAsync(int idx, string question, string key)
    {
        try
        {
            string url  = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={key}";
            string body = $"{{\"contents\":[{{\"parts\":[{{\"text\":{JsonSerializer.Serialize(question)}}}]}}]}}";
            var resp = await _httpClient.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            string json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            { SetResponse(idx, $"{Loc.T("ai_err")} Gemini ({(int)resp.StatusCode}): {json}", BrushError); return; }

            using var doc = JsonDocument.Parse(json);
            string text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content")
                .GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
            SetResponse(idx, text, null);
        }
        catch (Exception ex) { SetResponse(idx, $"{Loc.T("ai_err")}: {ex.Message}", BrushError); }
    }

    private async Task AskDeepSeekAsync(int idx, string question, string key)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    model = "deepseek-chat",
                    messages = new[] { new { role = "user", content = question } },
                    stream = false
                }), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Authorization", $"Bearer {key}");

            var resp = await _httpClient.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            { SetResponse(idx, $"{Loc.T("ai_err")} DeepSeek ({(int)resp.StatusCode}): {json}", BrushError); return; }

            using var doc = JsonDocument.Parse(json);
            string text = doc.RootElement.GetProperty("choices")[0].GetProperty("message")
                .GetProperty("content").GetString() ?? "";
            SetResponse(idx, text, null);
        }
        catch (Exception ex) { SetResponse(idx, $"{Loc.T("ai_err")}: {ex.Message}", BrushError); }
    }

    private async Task AskPerplexityAsync(int idx, string question, string key)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.perplexity.ai/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    model = "llama-3.1-sonar-small-128k-online",
                    messages = new[] { new { role = "user", content = question } }
                }), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Authorization", $"Bearer {key}");

            var resp = await _httpClient.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            { SetResponse(idx, $"{Loc.T("ai_err")} Perplexity ({(int)resp.StatusCode}): {json}", BrushError); return; }

            using var doc = JsonDocument.Parse(json);
            string text = doc.RootElement.GetProperty("choices")[0].GetProperty("message")
                .GetProperty("content").GetString() ?? "";
            SetResponse(idx, text, null);
        }
        catch (Exception ex) { SetResponse(idx, $"{Loc.T("ai_err")}: {ex.Message}", BrushError); }
    }

    private void SetResponse(int idx, string text, WMedia.Brush? brush)
    {
        Dispatcher.Invoke(() => { aiRows[idx].ResponseBrush = brush ?? ThemeText; aiRows[idx].Response = text; });
    }

    private void SaveAllResponses()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Loc.T("ai_save_header")} — {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine(new string('═', 60));
        sb.AppendLine($"{Loc.T("ai_question")} {txAiQuestion.Text.Trim()}");
        sb.AppendLine();

        bool hasAny = false;
        foreach (var r in aiRows)
        {
            string txt = r.Response.Trim();
            if (string.IsNullOrEmpty(txt)) continue;
            sb.AppendLine(new string('─', 60));
            sb.AppendLine($"■ {r.Name}");
            sb.AppendLine();
            sb.AppendLine(txt);
            sb.AppendLine();
            hasAny = true;
        }

        if (!hasAny)
        {
            MessageBox.Show(Loc.T("ai_nothing_msg"), Loc.T("ai_empty_title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SaveToFile(sb.ToString(), $"AI_{DateTime.Now:yyyyMMdd_HHmm}.txt");
    }

    private void SaveSingleResponse(AiRow r)
    {
        string txt = r.Response.Trim();
        if (string.IsNullOrEmpty(txt))
        {
            MessageBox.Show(Loc.T("ai_nothing_msg"), Loc.T("ai_empty_title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var sb = new StringBuilder();
        sb.AppendLine($"{r.Name} — {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine(new string('═', 60));
        sb.AppendLine($"{Loc.T("ai_question")} {txAiQuestion.Text.Trim()}");
        sb.AppendLine();
        sb.AppendLine(txt);
        SaveToFile(sb.ToString(), $"{r.Name}_{DateTime.Now:yyyyMMdd_HHmm}.txt");
    }

    private static void SaveToFile(string content, string fileName)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TakingMedications");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content, Encoding.UTF8);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    private void ShowApiKeyDialog()
    {
        var dlg = new Window
        {
            Title = Loc.T("ai_keys_title"),
            Width = 580, Height = 430,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = Frozen(30, 38, 48)
        };

        var panel = new StackPanel { Margin = new Thickness(16) };

        TextBox Row(string label, string linkText, string linkUrl, string value)
        {
            panel.Children.Add(new TextBlock
            {
                Text = label, FontWeight = FontWeights.Bold, FontSize = 13,
                Foreground = BrushLight, Margin = new Thickness(0, 8, 0, 2)
            });
            var tx = new TextBox { Text = value, FontSize = 13, Height = 30, Padding = new Thickness(4, 3, 4, 3) };
            panel.Children.Add(tx);

            var link = new TextBlock { Margin = new Thickness(0, 3, 0, 4), FontSize = 11 };
            var hl = new Hyperlink(new Run(linkText)) { NavigateUri = new Uri(linkUrl) };
            hl.RequestNavigate += (_, e) => Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            link.Inlines.Add(hl);
            panel.Children.Add(link);
            return tx;
        }

        var tc = Row("Claude (Anthropic) API:", Loc.T("ai_key_claude_link"), "https://console.anthropic.com/settings/keys", _claudeApiKey);
        var tg = Row("Gemini API:",     Loc.T("ai_key_gemini_link"),     "https://aistudio.google.com/apikey",     _geminiApiKey);
        var td = Row("DeepSeek API:",   Loc.T("ai_key_deepseek_link"),   "https://platform.deepseek.com/api_keys", _deepSeekApiKey);
        var tp = Row("Perplexity API:", Loc.T("ai_key_perplexity_link"), "https://www.perplexity.ai/settings/api", _perplexityApiKey);

        var btnOk = new Button
        {
            Content = Loc.T("ai_keys_save"), Width = 140, Height = 36,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0),
            FontWeight = FontWeights.Bold, IsDefault = true
        };
        btnOk.Click += (_, _) =>
        {
            _claudeApiKey = tc.Text.Trim(); _geminiApiKey = tg.Text.Trim();
            _deepSeekApiKey = td.Text.Trim(); _perplexityApiKey = tp.Text.Trim();
            SaveAiSettings();
            dlg.DialogResult = true;
        };
        panel.Children.Add(btnOk);

        dlg.Content = panel;
        dlg.ShowDialog();
    }

    private void LoadAiSettings()
    {
        string path = Path.Combine(AiDataDir, "ai_settings.json");
        if (!File.Exists(path)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (root.TryGetProperty("ClaudeKey",     out var c)) _claudeApiKey     = c.GetString() ?? "";
            if (root.TryGetProperty("GeminiKey",     out var g)) _geminiApiKey     = g.GetString() ?? "";
            if (root.TryGetProperty("DeepSeekKey",   out var d)) _deepSeekApiKey   = d.GetString() ?? "";
            if (root.TryGetProperty("PerplexityKey", out var p)) _perplexityApiKey = p.GetString() ?? "";
        }
        catch { }
    }

    private void SaveAiSettings()
    {
        Directory.CreateDirectory(AiDataDir);
        string path = Path.Combine(AiDataDir, "ai_settings.json");
        var obj = new
        {
            ClaudeKey = _claudeApiKey, GeminiKey = _geminiApiKey,
            DeepSeekKey = _deepSeekApiKey, PerplexityKey = _perplexityApiKey
        };
        File.WriteAllText(path, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
    }
}
