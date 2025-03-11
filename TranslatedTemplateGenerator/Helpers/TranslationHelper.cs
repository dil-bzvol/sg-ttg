using System.Text.Json;
using YamlDotNet.Serialization;

namespace TranslatedTemplateGenerator.Helpers;

/// <summary>
/// Provides helper methods for translation files.
/// </summary>
internal static class TranslationHelper
{
    /// <summary>
    /// Parses the translation file asynchronously.
    /// </summary>
    /// <param name="file">The translation file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parsed translation dictionary.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The translation file extension is not supported.</exception>
    internal static async Task<Dictionary<string, dynamic>> ParseTranslationFileAsync(
        IFormFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        
        var content = await reader.ReadToEndAsync(cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameof(content));

        var extension = Path.GetExtension(file.FileName);
        var parsed = extension switch
        {
            ".json" => JsonSerializer.Deserialize<Dictionary<string, dynamic>>(content),
            ".yml" or ".yaml" => new DeserializerBuilder().Build().Deserialize<Dictionary<string, dynamic>>(content),
            _ => throw new InvalidOperationException($"Unsupported translation file extension: {extension}")
        };

        if (parsed == null)
            throw new InvalidOperationException("Failed to parse translation file");

        return parsed;
    }
}

/// <summary>
/// Provides extension methods for translation dictionaries.
/// </summary>
internal static class TranslationDictionaryExtensions
{
    /// <summary>
    /// Gets the translation value for the given key.
    /// </summary>
    /// <param name="translations">The translations dictionary.</param>
    /// <param name="key">The translation key.</param>
    /// <returns>The translation value if found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="translations"/> is <see langword="null"/>, or
    /// <paramref name="key"/> is <see langword="null"/> or empty.
    /// </exception>
    internal static string? GetTranslationForKey(this Dictionary<string, dynamic>? translations, string key)
    {
        ArgumentNullException.ThrowIfNull(translations);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
            
        var keyParts = key.Split('.');
        dynamic current = translations;
        
        foreach (var keyPart in keyParts)
        {
            if (!current.ContainsKey(keyPart)) return null;
            current = current[keyPart];

            switch (current)
            {
                case JsonElement { ValueKind: JsonValueKind.String } jsonElement:
                    return jsonElement.GetString();
                case string translation:
                    return translation;
            }
        }
        
        return null;
    }
}