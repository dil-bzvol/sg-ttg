using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using NLog.Web;
using TranslatedTemplateGenerator.Middlewares;
using TranslatedTemplateGenerator.Models;
using TranslatedTemplateGenerator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddScoped<ITranslationService, TranslationService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHeaderPropagation();
builder.Services.AddAntiforgery();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseErrorHandling();

app.UseAntiforgery();

app.MapGet("/antiforgery/token", (HttpContext context, [FromServices] IAntiforgery antiforgery) =>
{
    var tokenSet = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { Token = tokenSet.RequestToken });
});

app.MapPost("/translate", async (
    [FromForm] TranslateRequest request,
    CancellationToken cancellationToken,
    [FromServices] ITranslationService translationService) =>
{
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(request);
    if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
        return Results.BadRequest(validationResults);

    var generatedTemplateIds = await translationService.TranslateAsync(
        request.SendGridApiKey,
        request.TemplateId,
        request.VersionId,
        request.Files,
        cancellationToken);

    return Results.Ok(new TranslateResponse { GeneratedTemplates = generatedTemplateIds });
});

app.Run();