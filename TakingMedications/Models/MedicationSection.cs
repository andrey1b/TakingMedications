using System.Collections.Generic;
using Newtonsoft.Json;

namespace TakingMedications.Models;

/// <summary>
/// Группа препаратов (УТРО / ДЕНЬ / ВЕЧЕР / СОН / SOS).
/// Соответствует объекту в medications.json.
/// </summary>
public class MedicationSection
{
    [JsonProperty("section")]     public string Title      { get; set; } = "";
    [JsonProperty("section_key")] public string SectionKey { get; set; } = "";
    [JsonProperty("items")]       public List<Medication> Items { get; set; } = new();

    public static IReadOnlyList<MedicationSection> CreateDefaults() => new[]
    {
        new MedicationSection { SectionKey = "morning", Title = "УТРО (завтрак 09:00 - 10:00)" },
        new MedicationSection { SectionKey = "day",     Title = "ДЕНЬ (обед 14:00 - 15:00)"    },
        new MedicationSection { SectionKey = "evening", Title = "ВЕЧЕР (ужин 18:00 - 19:00)"   },
        new MedicationSection { SectionKey = "night",   Title = "СОН (перед сном 23:00)"        },
        new MedicationSection { SectionKey = "sos",     Title = "ПО НЕОБХОДИМОСТИ (SOS)"        },
    };
}
