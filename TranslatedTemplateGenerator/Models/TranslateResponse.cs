namespace TranslatedTemplateGenerator.Models;

/// <summary>
/// Represents a response for a translation request.
/// </summary>
public record TranslateResponse
{
    /// <summary>
    /// The generated templates' IDs.
    /// </summary>
    public string[] GeneratedTemplates { get; init; }
}