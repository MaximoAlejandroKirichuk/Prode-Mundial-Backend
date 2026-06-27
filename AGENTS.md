# Repository guide

## Stack and shape
- This repo is a minimal ASP.NET Core Web API on .NET 10 (`Api-Prode-Mundial.sln` + `Api/Api.csproj`).
- It is still the default WeatherForecast scaffold: `Api/Program.cs`, `Api/Controllers/WeatherForecastController.cs`, and `Api/WeatherForecast.cs` are template code, not domain architecture.
- Real app wiring starts in `Api/Program.cs`; controllers live under `Api/Controllers/`.

## Verified commands
- Build from repo root: `dotnet build Api-Prode-Mundial.sln`
- Run from repo root: `dotnet run --project Api/Api.csproj`
- Default dev HTTP URL is `http://localhost:5183` via `Api/Properties/launchSettings.json`.
- Manual smoke test: use `Api/Api.http` against `GET http://localhost:5183/weatherforecast/`.

## Runtime and config quirks
- OpenAPI is only mapped in Development (`app.MapOpenApi()` inside the `IsDevelopment()` block), so verify `/openapi/v1.json` only in dev runs.
- `Api/Api.csproj` enables nullable reference types and implicit usings; keep new code compatible with both.

## What is not here yet
- No test project exists yet; do not guess test commands from other .NET repos.
- No CI workflows, Docker files, repo-local OpenCode config, or other agent instruction files are present.
- No database, auth, services layer, or migrations are wired yet; avoid documenting or assuming those conventions until they exist.
