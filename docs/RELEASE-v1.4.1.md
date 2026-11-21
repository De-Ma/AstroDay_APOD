# AstroDay v1.4.1

## APOD reliability

- Missing, future, video, and unsupported-media dates no longer interrupt browsing with error dialogs.
- NASA API 429 rate limits immediately fall back to NASA's website article/image scraper.
- APOD images load from trusted mission/content asset paths outside the traditional year/month folders.
- Reused historical APOD images are recognized while unrelated article images remain excluded.
- 2026-08-12 uses WordPress search/article-ID fallback when NASA's normal search omits it.
- 2026-10-25, 2026-10-31, 2026-11-11, and 2026-11-16 resolve to verified still images.

## UI fixes

- Favorited library buttons show **Remove favorite** on hover.
- Desktop and lock-screen actions now always use the image active in the current Today or Library view.
- A stale library selection no longer overrides the displayed Today image.

## Validation

- 66 automated tests pass.
- Release build completes with no warnings or errors.
- Includes installer and portable Windows x64 build.
