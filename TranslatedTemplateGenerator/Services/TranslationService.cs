using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SendGrid;
using TranslatedTemplateGenerator.Helpers;

namespace TranslatedTemplateGenerator.Services;



/// <inheritdoc />
public partial class TranslationService(ILogger<TranslationService> logger) : ITranslationService
{
    private const string TranslationKeyPattern = @"\[{2}[ \t]*(?<tlkey>[\w-.]+)[ \t]*\]{2}";
    private const string LangCodePattern = @"([\w-.]+\.|^)(?<lang>[a-z]{2}(-[a-z]{2})?)\.\w+$";
    private const string DefaultVersionName = "Default version";

    /// <inheritdoc />
    public async Task TranslateAsync(
        string sendGridApiKey,
        string templateId,
        string versionId,
        IFormFileCollection files,
        CancellationToken cancellationToken)
    {
        var client = new SendGridClient(sendGridApiKey);

        var templateResponse = await client.RequestAsync(
            BaseClient.Method.GET,
            urlPath: $"templates/{templateId}",
            cancellationToken: cancellationToken);

        if (!templateResponse.IsSuccessStatusCode)
            throw new InvalidOperationException("Failed to retrieve template");

        var template = await templateResponse.DeserializeResponseBodyAsync();

        var templateVersion = (template["versions"] as JArray)!
            .FirstOrDefault(version => version["id"]?.Value<string>() == versionId);
        if (templateVersion == null)
            throw new InvalidOperationException("Template version not found");

        var content = templateVersion["html_content"]?.Value<string>();
        if (string.IsNullOrEmpty(content))
            throw new InvalidOperationException("Template version content is empty");

        var translationKeys = TranslationKeyRegex().Matches(content)
            .Select(m => m.Groups["tlkey"].Value).ToList();
        if (translationKeys.Count == 0)
            throw new InvalidOperationException("The template does not contain any translation keys");

        var subject = templateVersion["subject"]?.Value<string>();

        var subjectTranslationKeys = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject))
            subjectTranslationKeys.AddRange(TranslationKeyRegex().Matches(subject)
                .Select(m => m.Groups["tlkey"].Value));

        var translatedTemplates = new Dictionary<string, Translation>();

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];

            var langCodeMatch = LangCodeRegex().Match(file.FileName);
            var langCode = "translation " +
                           (langCodeMatch.Success ? langCodeMatch.Groups["lang"].Value : (i + 1).ToString());

            var translations = await TranslationHelper.ParseTranslationFileAsync(file, cancellationToken);

            var translatedSubject = subject;
            foreach (var key in subjectTranslationKeys)
            {
                var translationValue = translations.GetTranslationForKey(key);
                if (translationValue == null)
                {
                    logger.LogDebug("Translation for key {Key} not found in file {FileName}", key, file.FileName);
                    continue;
                }

                var regex = GetTranslationKeyRegexForKey(key);
                translatedSubject = regex.Replace(translatedSubject!, translationValue);
            }

            var translatedContent = content;
            foreach (var key in translationKeys)
            {
                var translationValue = translations.GetTranslationForKey(key);
                if (translationValue == null)
                {
                    logger.LogDebug("Translation for key {Key} not found in file {FileName}", key, file.FileName);
                    continue;
                }

                var regex = GetTranslationKeyRegexForKey(key);
                translatedContent = regex.Replace(translatedContent, translationValue);
            }

            translatedTemplates.Add(langCode, new Translation(translatedContent, translatedSubject));
        }

        var uploadTasks = translatedTemplates
            .OrderBy(kvp => kvp.Key)
            .Select(async kvp =>
            {
                var templateName = $"{template["name"] as string} - {kvp.Key}";
                var templateCreationPayload = new
                {
                    Name = templateName,
                    Generation = "dynamic"
                };

                var jsonSerializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                };
                var requestBody = JsonSerializer.Serialize(templateCreationPayload, jsonSerializerOptions);

                var templateCreationResponse = client.RequestAsync(
                    BaseClient.Method.POST,
                    urlPath: "templates",
                    requestBody: requestBody,
                    cancellationToken: cancellationToken);

                if (!templateCreationResponse.Result.IsSuccessStatusCode)
                {
                    logger.LogError("Failed to create template {TemplateName}", templateName);
                    return false;
                }

                var createdTemplate = await templateCreationResponse.Result.DeserializeResponseBodyAsync();
                var createdTemplateId = createdTemplate["id"] as string;

                var versionCreationPayload = new
                {
                    Name = DefaultVersionName,
                    HtmlContent = kvp.Value.Content,
                    Subject = kvp.Value.Subject ?? string.Empty,
                    Editor = "design"
                };
                requestBody = JsonSerializer.Serialize(versionCreationPayload, jsonSerializerOptions);

                var versionCreationResponse = client.RequestAsync(
                    BaseClient.Method.POST,
                    urlPath: $"templates/{createdTemplateId}/versions",
                    requestBody: requestBody,
                    cancellationToken: cancellationToken);

                if (!versionCreationResponse.Result.IsSuccessStatusCode)
                    logger.LogError("Failed to create version for template {TemplateName}", templateName);

                return versionCreationResponse.Result.IsSuccessStatusCode;
            }).ToList();

        await Task.WhenAll(uploadTasks);

        var totalCount = uploadTasks.Count;
        var successCount = uploadTasks.Count(t => t.Result);
        logger.LogInformation("Successfully uploaded {SuccessCount} out of {TotalCount} translations",
            successCount, totalCount);
    }

    private static Regex GetTranslationKeyRegexForKey(string key) =>
        new(@"\[{2}[ \t]*" + key + @"[ \t]*\]{2}", RegexOptions.IgnoreCase);

    [GeneratedRegex(TranslationKeyPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TranslationKeyRegex();

    [GeneratedRegex(LangCodePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LangCodeRegex();

    private record Translation(string Content, string? Subject);
}