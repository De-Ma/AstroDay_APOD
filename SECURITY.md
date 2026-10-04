# Security Policy

## Supported version

Security fixes are intended for the latest released version of AstroDay.

## Reporting a concern

Please do not publish API keys, personal data, or exploitable details in a public issue. Until a private security contact is configured for the GitHub repository, use GitHub's private vulnerability reporting feature if it is enabled; otherwise report the issue without secrets and request a private contact route.

## Security notes

- NASA API keys are protected with Windows DPAPI for the current Windows user. This protects the key at rest from other users, but not from malware or processes running as the same user.
- Existing plaintext API keys in `%LOCALAPPDATA%\\AstroDay\\settings.json` are migrated to DPAPI-protected storage when settings are next loaded and saved.
- APOD image downloads require HTTPS and NASA-owned hosts, and are limited to 100 MB per image.
- Settings and library indexes are written through temporary files and atomically replaced where the filesystem supports it.
- The app does not expose a network listener and does not send telemetry. NASA receives the configured API key with API requests.
- Do not commit `settings.json`, API keys, personal library data, build output, or logs. The repository `.gitignore` excludes common generated and secret-bearing files.

Report suspected key exposure by revoking the key with NASA and creating a new one.
