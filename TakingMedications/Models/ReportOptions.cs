using System;

namespace TakingMedications.Models;

/// <summary>
/// Параметры экспорта PDF-отчёта.
/// </summary>
public class ReportOptions
{
    public DateTime From { get; set; } = DateTime.Today.AddDays(-29);
    public DateTime To   { get; set; } = DateTime.Today;

    public bool IncludeSchedule { get; set; } = true;
    public bool IncludeHistory  { get; set; } = true;
    public bool IncludePressure { get; set; } = true;
    public bool IncludeFinance  { get; set; } = true;

    public string? PatientName { get; set; }
}
