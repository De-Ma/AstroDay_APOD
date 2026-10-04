# AstroDay Session Handoff

Updated: 2026-10-04

## Project

- Windows-only .NET 10 WPF app for NASA APOD; target framework `net10.0-windows10.0.19041.0`. Cross-platform is out of scope.
- Use `C:\Users\denni\.dotnet10\dotnet.exe` (SDK 10.0.401). `dotnet` on PATH resolves to SDK 8, while `global.json` requires 10.0.100.
- Git is initialized on `main` with no commits and no remote; everything is untracked. MIT license; copyright holder `AstroDay contributors` still to be confirmed.

## Completed 2026-10-04

Root cause of the "pixelated NASA image" since 2026-09-29: NASA moved APOD to `science.nasa.gov/apod/`; the old scraper picked the NASA header logo, and old `apod.nasa.gov/apod/apYYMMDD.html` URLs now redirect to today's page. The shared `DEMO_KEY` also returns HTTP 429, which triggered the fallback.

- **Image sourcing** (`Services/ApodService.cs`, `Services/ApodParsing.cs`, `Models/ApodSources.cs`): three sources (API, WordPress article search via `science.nasa.gov/wp-json/wp/v2/image-article`, today's webpage), tried in a user-chosen order (`AppSettings.SourceOrder`, persisted). The legacy logo-prone scrape was removed.
- **Time zone:** "today" is the US Eastern date (`ApodParsing.ApodToday`), not the local date.
- **Overwrite bug fixed:** page date is read only from the page's DATE row; an already-stored day is not overwritten by the page source unless Reload is forced.
- **Text-only days:** videos are saved as entries with title and explanation and an empty `FilePath` (`ApodItem.HasImage == false`); they are skipped for wallpaper, lock screen and favorite rotation.
- **Library UI:** three-dot menu (Go to APOD, Open file location, Reload image, Remove from library), double-click opens the image (or APOD page for text-only), Open folder button, square-cornered cards with fixed button row, thumbnail cache, themed context menu.
- **Today tab:** Reload and Go to APOD buttons, double-click preview, selectable title/description (read-only TextBoxes), preview refreshes immediately when changing day, stale-refresh guard.
- **Scheduling:** check at startup and every 6 hours; every 30 minutes until today's APOD is available (`App.xaml.cs`).
- **Settings redesign:** sectioned layout, toggle switches instead of checkboxes, image-source order list, plain-text library path, six accents (Mint, Sky, Violet, Amber, Coral, Lime) in a 3x2 grid.
- **Other:** loader planets spin clockwise with wider orbits and restart each time; maximized window respects the taskbar (`Services/WorkAreaFix.cs`); themed date picker; primary-styled Set desktop / Set lock screen buttons; UI stays responsive during reload (async decode).
- **Wrap-up cleanup:** removed unused `IsRasterUrl`, an unused variable, and the obsolete `UseWebsiteFallback` setting; fixed a bug where `SourceOrder` was not saved (the settings DTO in `SettingsService` lacked it); ran `dotnet format`.

## Current status

- `dotnet format --verify-no-changes`: pass. Release build: 0 warnings, 0 errors. Tests: 45 passed.
- `ApodParsing.cs`, `LibraryService.cs` and `ApodSources.cs` are linked into `tests/AstroDay.Tests.csproj`; new source files used by tests must be added there too.
- The app was last launched from `bin\Debug` without startup errors.

## Known issues

- Window code, the source fallback loop in `ApodService`, and the scheduler have no automated tests (they need HTTP/UI seams).
- The reported accent-colour "different style" issue was not fully understood; check the settings look.
- Go to APOD for old dates relies on article search; the fallback archive page (`/apod/archive/`) only lists about 15 recent days.
- Library entries created by earlier buggy builds may still hold NASA-logo images; use Reload or Remove from library.
- The order of switched-off sources is not persisted (they return to the end of the list).
- When api.nasa.gov hangs, the API source (3 attempts x 25 s) delays fallback to the next source by over a minute; consider a shorter API timeout or fewer retries.
- On 2026-10-04 the API returned the NASA logo as the image for the new day; `ApodParsing.IsPlaceholderImageUrl` now rejects logo/theme URLs so the next source is tried.
- Windows Code Integrity previously blocked an unsigned build (policy `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`); `CiTool.exe -lp` must be run elevated to inspect it. Debug builds under `bin\Debug` have run fine.

## Next recommended task

1. Add an `HttpMessageHandler` seam in `ApodService` and tests for source ordering, 429 fallback, and video days.
2. Before the first commit: review untracked files for secrets/user images, set the Git author, and confirm the MIT copyright holder.
3. Prepare a signed or policy-accepted release artifact.
