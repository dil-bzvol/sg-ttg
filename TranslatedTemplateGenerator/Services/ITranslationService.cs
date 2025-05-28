namespace TranslatedTemplateGenerator.Services;

/// <summary>
/// Represents a service that generates translations of SendGrid templates.
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Generates translated templates from the given template and translation files.
    /// </summary>
    /// <param name="sendGridApiKey">The API key used to authorize requests to the SendGrid API.</param>
    /// <param name="templateId">The ID of the template to translate.</param>
    /// <param name="versionId">The ID of the template version to translate.</param>
    /// <param name="files">The translation files.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A dictionary containing pairs of translation IDs and their corresponding generated template IDs.</returns>
    public Task<Dictionary<string, string?>> TranslateAsync(
        string sendGridApiKey,
        string templateId,
        string versionId,
        IFormFileCollection files,
        CancellationToken cancellationToken);
}