using Newtonsoft.Json;

namespace TakingMedications.Models;

/// <summary>
/// Один препарат. Поля совпадают с Python-схемой
/// (см. Medication/medications.json и med_edit_dialog.py).
/// </summary>
public class Medication
{
    [JsonProperty("id")]          public string Id          { get; set; } = "";
    [JsonProperty("time")]        public string Time        { get; set; } = "";
    [JsonProperty("time_note")]   public string TimeNote    { get; set; } = "";
    [JsonProperty("name")]        public string Name        { get; set; } = "";
    [JsonProperty("subtitle")]    public string Subtitle    { get; set; } = "";
    [JsonProperty("note")]        public string Note        { get; set; } = "";
    [JsonProperty("doctor")]      public string Doctor      { get; set; } = "";
    [JsonProperty("course")]      public string Course      { get; set; } = "";
    [JsonProperty("course_type")] public string CourseType  { get; set; } = "";
    [JsonProperty("pharmacy")]    public string Pharmacy    { get; set; } = "";
    [JsonProperty("description")] public string Description { get; set; } = "";

    [JsonIgnore] public string? SectionKey { get; set; }

    public Medication Clone() => new()
    {
        Id = Id, Time = Time, TimeNote = TimeNote, Name = Name,
        Subtitle = Subtitle, Note = Note, Doctor = Doctor,
        Course = Course, CourseType = CourseType, Pharmacy = Pharmacy,
        Description = Description, SectionKey = SectionKey
    };
}
