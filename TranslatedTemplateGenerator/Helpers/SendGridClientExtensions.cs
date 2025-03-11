using System.Text.Json;
using SendGrid;

namespace TranslatedTemplateGenerator.Helpers;

/// <summary>
/// Extension methods for <see cref="SendGridClient"/>.
/// </summary>
internal static class SendGridClientExtensions
{
    private const string DynamicTemplateGeneration = "dynamic";
    private const string DesignEditor = "design";
    private const string DefaultVersionName = "Default version";

    private static readonly JsonSerializerOptions UploadJsonSerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>
    /// Retrieves a single template.
    /// </summary>
    /// <param name="client">The <see cref="SendGridClient"/>.</param>
    /// <param name="templateId">The ID of the template to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retrieved template.</returns>
    /// <exception cref="ArgumentException">The template ID is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Failed to retrieve template.</exception>
    internal static async Task<Dictionary<string, dynamic>> GetTemplateAsync(
        this SendGridClient client,
        string templateId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);

        var templateResponse = await client.RequestAsync(
            BaseClient.Method.GET,
            urlPath: $"templates/{templateId}",
            cancellationToken: cancellationToken);

        if (!templateResponse.IsSuccessStatusCode)
            throw new InvalidOperationException("Failed to retrieve template");

        var template = await templateResponse.DeserializeResponseBodyAsync();
        if (template == null)
            throw new InvalidOperationException("Failed to deserialize template");

        return template;
    }

    /// <summary>
    /// Creates a new template.
    /// </summary>
    /// <param name="client">The <see cref="SendGridClient"/>.</param>
    /// <param name="name">The name of the template to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created template.</returns>
    /// <exception cref="ArgumentException">The name is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Failed to create template.</exception>
    internal static async Task<Dictionary<string, dynamic>> CreateTemplateAsync(
        this SendGridClient client,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var response = await client.RequestAsync(
            BaseClient.Method.POST,
            urlPath: "templates",
            requestBody: JsonSerializer.Serialize(new
            {
                Name = name,
                Generation = DynamicTemplateGeneration,
            }, UploadJsonSerializerOptions),
            cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Failed to create template");

        var template = await response.DeserializeResponseBodyAsync();
        if (template == null)
            throw new InvalidOperationException("Failed to deserialize created template");

        return template;
    }
    
    /// <summary>
    /// Creates a template version.
    /// </summary>
    /// <param name="client">The <see cref="SendGridClient"/>.</param>
    /// <param name="templateId">The ID of the template to create a version for.</param>
    /// <param name="name">The name of the version to create.</param>
    /// <param name="subject">The subject of the version to create.</param>
    /// <param name="htmlContent">The content of the version to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created template version.</returns>
    /// <exception cref="ArgumentException">The template ID is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The HTML content is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Failed to create version for template.</exception>
    internal static async Task<Dictionary<string, dynamic>> CreateTemplateVersionAsync(
        this SendGridClient client,
        string templateId,
        string? name,
        string? subject,
        string htmlContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentNullException.ThrowIfNull(htmlContent);

        var response = await client.RequestAsync(
            BaseClient.Method.POST,
            urlPath: $"templates/{templateId}/versions",
            requestBody: JsonSerializer.Serialize(new
            {
                Name = name ?? DefaultVersionName,
                Subject = subject ?? string.Empty,
                HtmlContent = htmlContent,
                Editor = DesignEditor
            }, UploadJsonSerializerOptions),
            cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Failed to create version for template");

        var templateVersion = await response.DeserializeResponseBodyAsync();
        if (templateVersion == null)
            throw new InvalidOperationException("Failed to deserialize created version");

        return templateVersion;
    }
}