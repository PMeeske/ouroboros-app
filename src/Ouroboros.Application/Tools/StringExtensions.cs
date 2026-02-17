namespace Ouroboros.Application.Tools;

/// <summary>
/// String extension methods for the Application namespace.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// Truncates a string to the specified maximum length.
    /// </summary>
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}