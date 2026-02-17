namespace Ouroboros.CLI.Services;

/// <summary>
/// Represents skill information
/// </summary>
public class SkillInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float SuccessRate { get; set; }
}