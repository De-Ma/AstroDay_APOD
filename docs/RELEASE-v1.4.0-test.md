# AstroDay v1.4.0 test release

This is a test prerelease for trying the APOD edge-case fixes before a stable v1.4 release.

## Fixed

- APOD dates with videos, missing content, unsupported media, or future dates no longer produce an error popup while browsing.
- NASA images stored outside the traditional `apod/{year}/{month}` folders now load correctly.
- The reported 2026-10-31 and 2026-11-11 images are recognized and downloaded.
- NASA API HTTP 429 rate limits now fall through immediately to the NASA website article/image scraper.
- Future/unpublished dates remain an inline no-still-image state.

## Validation

- 60 automated tests pass.
- Release build completes with no warnings or errors.
- Includes installer and portable Windows x64 build.
