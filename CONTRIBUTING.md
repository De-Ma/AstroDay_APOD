# Contributing

## Development setup

- Install the .NET 10 SDK on Windows. The repository's `global.json` requires the 10.0.100 baseline and rolls forward to later .NET 10 feature bands.
- If installing the SDK under your user profile, add that SDK directory to your user `PATH` so `dotnet` commands can find it.
- Restore with `dotnet restore AstroDay.sln`.
- Run the app with `dotnet run --project AstroDay.csproj`.

## Before submitting a change

Run these checks from the repository root:

```powershell
dotnet format AstroDay.sln --verify-no-changes --no-restore
dotnet test AstroDay.sln --configuration Release
```

Keep changes focused, add tests for behavior changes, and avoid committing generated output, local settings, API keys, or downloaded APOD images. UI behavior should be checked on Windows because AstroDay uses WPF, the Windows notification area, desktop personalization APIs, registry startup settings, and DPAPI.

By submitting a contribution you agree that it is licensed under the Apache License 2.0, as described in section 5 of the [LICENSE](LICENSE).
