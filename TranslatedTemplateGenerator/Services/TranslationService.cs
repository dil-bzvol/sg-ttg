using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SendGrid;
using TranslatedTemplateGenerator.Helpers;

namespace TranslatedTemplateGenerator.Services;

/*
 * TODO:
 * - Store generated template IDs (e.g. in SQLite DB)
 * - (Optionally) delete previously generated templates before uploading new ones
 */

/// <inheritdoc />
public partial class TranslationService(ILogger<TranslationService> logger) : ITranslationService
{
    private const string TranslationKeyPattern = @"\[{2}[ \t]*(?<tlkey>[\w-.]+)[ \t]*\]{2}";
    private const string LangCodePattern = @"([\w-.]+\.|^)(?<lang>[a-z]{2}(-[a-z]{2})?)\.\w+$";

    /// <inheritdoc />
    public async Task TranslateAsync(
        string sendGridApiKey,
        string templateId,
        string versionId,
        IFormFileCollection files,
        CancellationToken cancellationToken)
    {
        var client = new SendGridClient(sendGridApiKey);

        var template = await client.GetTemplateAsync(templateId, cancellationToken);
        var templateName = (template["name"] as string)!;

        var templateVersion = (template["versions"] as JArray)!
            .FirstOrDefault(version => version["id"]?.Value<string>() == versionId)
            ?.ToObject<Dictionary<string, dynamic>>();
        if (templateVersion == null)
            throw new InvalidOperationException("Template version not found");

        var subject = templateVersion["subject"] as string;
        var subjectTranslationKeys = GetSubjectTranslationKeys(subject);

        var content = templateVersion["html_content"] as string;
        if (string.IsNullOrEmpty(content))
            throw new InvalidOperationException("Template version content is empty");
        var contentTranslationKeys = GetContentTranslationKeys(content);

        var translatedTemplates = await Task.WhenAll(files.Select(async (file, index) =>
            await Translate(
                file, index,
                subject, subjectTranslationKeys,
                content, contentTranslationKeys,
                cancellationToken
            )));

        var uploads = await UploadTranslatedTemplates(client, templateName, translatedTemplates, cancellationToken);

        var totalCount = uploads.Length;
        var successCount = uploads.Count(t => t != null);
        logger.LogInformation("Successfully uploaded {SuccessCount} out of {TotalCount} translations",
            successCount, totalCount);
    }

    private static List<string> GetContentTranslationKeys(string content)
    {
        var translationKeys = TranslationKeyRegex().Matches(content)
            .Select(m => m.Groups["tlkey"].Value).ToList();

        if (translationKeys.Count == 0)
            throw new InvalidOperationException("The template does not contain any translation keys");

        return translationKeys;
    }

    private static List<string> GetSubjectTranslationKeys(string? subject)
    {
        var subjectTranslationKeys = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject))
            subjectTranslationKeys.AddRange(TranslationKeyRegex().Matches(subject)
                .Select(m => m.Groups["tlkey"].Value));

        return subjectTranslationKeys;
    }

    private static async Task<Translation> Translate(
        IFormFile translationFile,
        int index,
        string? templateSubject,
        List<string> subjectTranslationKeys,
        string templateContent,
        List<string> contentTranslationKeys,
        CancellationToken cancellationToken)
    {
        var langCodeMatch = LangCodeRegex().Match(translationFile.FileName);
        var id = langCodeMatch.Success
            ? langCodeMatch.Groups["lang"].Value
            : (index + 1).ToString();

        var translations = await TranslationHelper.ParseTranslationFileAsync(translationFile, cancellationToken);

        var translatedSubject = templateSubject != null
            ? TranslateString(templateSubject, subjectTranslationKeys, translations)
            : null;
        var translatedContent = TranslateString(templateContent, contentTranslationKeys, translations);

        return new Translation(id, translatedContent, translatedSubject);
    }

    private static string TranslateString(
        string s,
        List<string> translationKeys,
        Dictionary<string, dynamic> translations)
    {
        foreach (var key in translationKeys)
        {
            var translationValue = translations.GetTranslationForKey(key);
            if (translationValue == null) continue;

            var regex = GetTranslationKeyRegexForKey(key);
            s = regex.Replace(s, translationValue);
        }

        return s;
    }

    private static Task<Translation[]> TranslateTemplates(
        IFormFileCollection files,
        string templateSubject,
        List<string> subjectTranslationKeys,
        string templateContent,
        List<string> contentTranslationKeys,
        CancellationToken cancellationToken) =>
        Task.WhenAll(files.Select((file, index) =>
            Translate(
                file, index,
                templateSubject, subjectTranslationKeys,
                templateContent, contentTranslationKeys,
                cancellationToken
            )));

    private static async Task<string?> UploadTranslatedTemplate(
        SendGridClient client,
        string templateName,
        Translation translation,
        CancellationToken cancellationToken)
    {
        var newTemplateName = $"{templateName} - translation {translation.Id}";

        try
        {
            var createdTemplate = await client.CreateTemplateAsync(newTemplateName, cancellationToken);
            var createdTemplateId = (createdTemplate["id"] as string)!;

            await client.CreateTemplateVersionAsync(
                createdTemplateId, null, translation.Subject, translation.Content, cancellationToken);

            return createdTemplateId;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static Task<string?[]> UploadTranslatedTemplates(
        SendGridClient client,
        string templateName,
        Translation[] translatedTemplates,
        CancellationToken cancellationToken) =>
        Task.WhenAll(translatedTemplates
            .OrderBy(translation => translation.Id)
            .Select(translation => UploadTranslatedTemplate(client, templateName, translation, cancellationToken))
        );

    private static Regex GetTranslationKeyRegexForKey(string key) =>
        new(@"\[{2}[ \t]*"
            + key.Replace(".", @"\.")
            + @"[ \t]*\]{2}", RegexOptions.IgnoreCase);

    [GeneratedRegex(TranslationKeyPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TranslationKeyRegex();

    [GeneratedRegex(LangCodePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LangCodeRegex();

    private record Translation(string Id, string Content, string? Subject);
}