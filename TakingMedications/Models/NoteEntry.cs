using Newtonsoft.Json;

namespace TakingMedications.Models;

public class NoteEntry
{
    [JsonProperty("text")]
    public string Text { get; set; } = "";

    [JsonProperty("created")]
    public string Created { get; set; } = "";

    [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)]
    public string? Color { get; set; }
}
