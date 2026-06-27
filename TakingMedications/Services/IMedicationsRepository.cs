using System.Collections.Generic;
using TakingMedications.Models;

namespace TakingMedications.Services;

public interface IMedicationsRepository
{
    List<MedicationSection> Load();
    void Save(IEnumerable<MedicationSection> sections);
}
