# AstroDay v1.4.1 test release

This prerelease adds more APOD edge-case fixes for user testing.

## Fixed

- 2026-10-25 now loads when the APOD article intentionally reuses an older image from the APOD asset archive.
- 2026-11-16 now loads its Webb mission image.
- 2026-08-12 now resolves through the WordPress search endpoint when NASA's image-article search omits the article.
- "Go to APOD" uses the same article lookup fallback for omitted search results.
- Primary article images remain prioritized, while unrelated older APOD images in the article are ignored.

## Validation

- 64 automated tests pass.
- Release build completes with no warnings or errors.
- Includes installer and portable Windows x64 build.
