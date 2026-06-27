using TakingMedications.Models;

namespace TakingMedications.Services;

public interface IStateRepository
{
    AppState Load();
    void Save(AppState state);
}
