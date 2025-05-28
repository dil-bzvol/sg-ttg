FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
LABEL org.opencontainers.image.source = "https://github.com/dil-bzvol/sg-ttg"
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TranslatedTemplateGenerator/TranslatedTemplateGenerator.csproj", "TranslatedTemplateGenerator/"]
RUN dotnet restore "TranslatedTemplateGenerator/TranslatedTemplateGenerator.csproj"
COPY . .
WORKDIR "/src/TranslatedTemplateGenerator"
RUN dotnet build "TranslatedTemplateGenerator.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "TranslatedTemplateGenerator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TranslatedTemplateGenerator.dll"]
