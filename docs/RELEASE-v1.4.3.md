# AstroDay v1.4.3

## Installer compatibility

- Removed the unsupported `userpictures` and `userprofile` installer constants.
- New installations now default the image library to `%LOCALAPPDATA%\AstroDay\Images`, using the Windows folder constant already required by AstroDay itself.
- Added a real clean-install and uninstall smoke test to the release workflow so runtime-only installer errors are caught before publishing.

## Dependencies

- The installer and portable build remain self-contained for Windows x64; no separate .NET installation is required.
- Supported Windows versions are Windows 10 version 2004 (build 19041) and newer, including Windows 11.
