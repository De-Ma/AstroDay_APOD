# AstroDay Session Handoff

Updated: 2026-11-21

## Project

- Windows-only .NET 10 WPF app for NASA APOD; target framework `net10.0-windows10.0.19041.0`. Cross-platform is out of scope.
- Use `%USERPROFILE%\.dotnet10\dotnet.exe` (SDK 10.0.401). `dotnet` on PATH resolves to SDK 8, while `global.json` requires 10.0.100.
- Git is on `main` with `De-Ma/AstroDay_APOD` configured as `origin`; author `DMA` (repo-local config). Licensed under Apache 2.0 (`LICENSE`, `NOTICE`, copyright DMA).

## Completed 2026-11-21 (v1.4.1)

- **No disruptive popups:** expected no-image, video, missing, future-date, and download/parser failures are shown inline while browsing or reloading dates and written to the local error log.
- **NASA asset parsing:** APOD still images are accepted from trusted mission/content asset paths outside the traditional `apod/{year}/{month}` folders. Date-specific APOD paths for another day are still rejected.
- **Reported dates:** 2026-10-31 and 2026-11-11 now resolve to the verified NASA JPEG assets. 2026-11-29 is a future/unpublished date as of 2026-11-21, so it returns the inline no-still-image state without a network search.
- **API rate limit fallback:** `ApodService` now treats NASA API HTTP 429 as recoverable, immediately continues to the website article scraper, and has an injected HTTP handler seam for deterministic fallback tests.
- **Tests:** parser coverage was added for mission-library assets, APOD assets without year/month folders, and expanded video/embed detection.
- **Stable release:** v1.4.1 promotes the tested APOD fixes; hyphenated versions remain available as GitHub prereleases.
- **Additional reported dates:** 2026-10-25 accepts a reused 2021 APOD hero image, 2026-11-16 accepts its Webb mission asset, and 2026-08-12 uses the WordPress search API/article-ID fallback when the image-article search omits it.
- **Post-test UI fixes:** favorited library buttons now show “Remove favorite” on hover. Manual desktop and lock-screen apply actions follow the active tab, so a stale library selection can no longer override the currently displayed Today image.

## Completed 2026-11-21 (v1.3.0)

- **Shortcut icon:** an original mint/orbit planet icon was added as a multi-resolution `.ico` and is embedded in the executable, installer, and shortcuts.
- **Library choice:** the installer has a dedicated image-library page. It preselects the existing registry/settings path on upgrade and configures `library.json` plus downloaded-image storage there.
- **Safe upgrades:** setup detects the installed version, presents update details, stops AstroDay before file replacement, and performs an in-place update through the same AppId.
- **Data preservation:** library files are outside the uninstall file list. Relocation preserves the old library and renames conflicting target images/catalogues instead of deleting or overwriting them.
- **Launch after install:** AstroDay starts normally by default after setup; the task can be unchecked.
- **Release:** prepared as v1.3.0 with version-specific release notes and dynamic release workflow note selection.

## Completed 2026-11-21 (v1.2.0)

- **Readable themed controls:** the wallpaper placement selector now uses a dark custom ComboBox template and the current accent throughout, including its popup.
- **Library selection:** selected cards use a thicker accent border and faint shadow.
- **Bounded fallback:** automatic APOD checks search today plus at most five prior days for a still image; after that the current desktop is kept.
- **Installer:** the Inno Setup installer supports a selectable per-user directory, optional Windows sign-in startup, optional launch minimized to the tray, and start-menu/desktop shortcuts.
- **Updates:** the Settings update area exposes repository/release links and a direct installer download when the latest release contains an installer asset.
- **Version visibility:** the window title bar shows the installed version badge.

## Completed 2026-11-21 (v1.1.0)

- **Library wallpaper action:** the three-dot card menu can set a still image as the desktop background/wallpaper.
- **Wallpaper placement:** desktop images use a persisted Fill or Fit placement setting; Fit preserves the whole image and aspect ratio, while Fill crops as needed.
- **Updates:** AstroDay checks the latest GitHub release at most once per day, offers manual checks in Settings and the tray, and opens the release page when an update is accepted.
- **Releases:** version `1.1.0` and `.github/workflows/release.yml` publish a self-contained Windows x64 zip and create a generated-notes GitHub release from matching `v*` tags.

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

- `dotnet format --verify-no-changes`: pass. Release tests: 66 passed. Release build: 0 warnings, 0 errors. Stable v1.4.1 package validation completed.
- `ApodParsing.cs`, `LibraryService.cs` and `ApodSources.cs` are linked into `tests/AstroDay.Tests.csproj`; new source files used by tests must be added there too.
- The app was last launched from `bin\Debug` without startup errors.

## Known issues

- Window code, the source fallback loop in `ApodService`, and the scheduler have no automated tests (they need HTTP/UI seams).
- Installer signing is not configured, so Windows may show an unsigned-publisher warning.
- Go to APOD for old dates relies on article search; the fallback archive page (`/apod/archive/`) only lists about 15 recent days.
- Library entries created by earlier buggy builds may still hold NASA-logo images; use Reload or Remove from library.
- The order of switched-off sources is not persisted (they return to the end of the list).
- When api.nasa.gov hangs, the API source (3 attempts x 25 s) delays fallback to the next source by over a minute; consider a shorter API timeout or fewer retries.
- On 2026-10-04 the API returned the NASA logo as the image for the new day; `ApodParsing.IsPlaceholderImageUrl` now rejects logo/theme URLs so the next source is tried.
- Windows Code Integrity previously blocked an unsigned build (policy `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`); `CiTool.exe -lp` must be run elevated to inspect it. Debug builds under `bin\Debug` have run fine.

## Next recommended task

1. Monitor the stable v1.4.1 release for APOD source edge cases and update the parser as new NASA layouts appear.
2. Consider code signing or another Code Integrity acceptance path for the distributed release artifact.
