namespace Ouroboros.Application.Services;

/// <summary>
/// Statistics about self-persistence.
/// </summary>
public class PersistenceStats
{
    public bool IsConnected { get; set; }
    public string CollectionName { get; set; } = "";
    public long TotalPoints { get; set; }
    public int FileBackups { get; set; }
}