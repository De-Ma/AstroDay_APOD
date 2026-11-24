# AstroDay v1.4.2

## Installer fix

- Fixed the installer startup error: "Internal error: Unknown constant `userpictures`".
- New installations now use the Pictures folder under the current user profile for the default AstroDay library.
- Existing installations continue to use the library path stored in the Windows registry or AstroDay settings.

## Validation

- Installer compilation succeeds with Inno Setup.
- Includes installer and portable Windows x64 build.
