using Newtonsoft.Json;

namespace TakingMedications.Models;

/// <summary>
/// Запись о покупке препарата. Хранится в `state["_purchases"]` (новый ключ
/// в C#-версии — в питон-версии финансы лежали в `_finance.cells`,
/// но мы оставляем тот блок как есть в RawExtras для обратной
/// совместимости и держим свой плоский журнал отдельно).
/// </summary>
public class PurchaseEntry
{
    [JsonProperty("ts")]      public string Date  { get; set; } = "";   // ISO YYYY-MM-DD
    [JsonProperty("medId")]   public string MedId { get; set; } = "";
    [JsonProperty("amount")]  public decimal Amount { get; set; }
    [JsonProperty("note", NullValueHandling = NullValueHandling.Ignore)]
    public string? Note { get; set; }
}
