using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace TakingMedications.Services;

enum UpdateCheckResult { UpdateFound, UpToDate, Failed }

static class Updater
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "TakingMedications" } }
    };

    public static async Task<UpdateCheckResult> CheckForUpdateAsync(string lang)
    {
        try
        {
            var release = await Http.GetFromJsonAsync<GhRelease>(
                "https://api.github.com/repos/andrey1b/TakingMedications/releases/latest");

            if (release is null) return UpdateCheckResult.Failed;

            var tag = release.TagName.TrimStart('v');
            if (!Version.TryParse(tag, out var latest)) return UpdateCheckResult.Failed;

            var cur = Assembly.GetExecutingAssembly().GetName().Version!;
            var current = new Version(cur.Major, cur.Minor, cur.Build);
            if (latest <= current) return UpdateCheckResult.UpToDate;

            string msg = lang == "en"
                ? $"Taking Medications {latest} is available.\nOpen the download page?"
                : $"Доступна новая версия Приём лекарств {latest}.\nОткрыть страницу загрузки?";

            var res = MessageBox.Show(msg, "Taking Medications",
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (res == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(release.HtmlUrl) { UseShellExecute = true });

            return UpdateCheckResult.UpdateFound;
        }
        catch { return UpdateCheckResult.Failed; }
    }

    private record GhRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")]  string HtmlUrl);
}
