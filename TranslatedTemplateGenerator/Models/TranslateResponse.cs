namespace TranslatedTemplateGenerator.Models;

/// <summary>
/// Represents a response for a translation request.
/// </summary>
public record TranslateResponse
{
    /// <summary>
    /// A dictionary containing pairs of translation IDs and their corresponding generated template IDs.
    /// </summary>
    public required Dictionary<string, string?> GeneratedTemplates { get; init; }
}