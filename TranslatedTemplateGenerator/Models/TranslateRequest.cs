using System.ComponentModel.DataAnnotations;

namespace TranslatedTemplateGenerator.Models;

/// <summary>
/// Represents a request to generate translated templates.
/// </summary>
public record TranslateRequest
{
    /// <summary>
    /// The API key used to authorize requests to the SendGrid API.
    /// </summary>
    [Required]
    public required string SendGridApiKey { get; init; }

    /// <summary>
    /// The ID of the template to translate.
    /// </summary>
    /// <example>d-9dd8fcef9530466ba771e22ba08b85df</example>
    [Required]
    public required string TemplateId { get; init; }
    
    /// <summary>
    /// The ID of the template version to translate.
    /// </summary>
    [Required]
    public required string VersionId { get; init; }

    /// <summary>
    /// The translation files.
    /// </summary>
    [Required]
    public required IFormFileCollection Files { get; init; }
};