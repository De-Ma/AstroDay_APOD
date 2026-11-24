# AstroDay v1.5.0

## Library

- Favorite changes update individual cards without flickering.
- Selected, hovered, and favorite states are visually distinct and consistent.
- Gallery padding keeps selection glow inside the scrollable content area.
- Thumbnails load asynchronously near the viewport and stay cached while scrolling.
- Shared-folder catalogues use portable image paths, tolerate incomplete sync writes, preserve missing items, and recover as files arrive.

## Installer and application

- Added a themed installer with selectable installation and library folders.
- Installation preferences provide the single **Install** action; AstroDay launches after Finish.
- Running AstroDay processes close automatically during upgrades.
- AstroDay now enforces a practical 960 by 650 minimum window size.
- Window, tray, desktop, installer, and loading visuals share a coherent orbital design.

## APOD reliability

- Hero-image selection is restricted to the dated article's main media.
- Catalogue/image file-size checks prevent stale synced images from being paired with newer metadata.
- Legacy catalogue entries without integrity metadata refresh automatically once.

## Validation

- 72 automated tests pass.
- Formatting, self-contained publish, installer compilation, and clean install/uninstall smoke tests pass.
