# CheapShark game discovery — October 13, 2026

Latest complete portable build: outputs/YAIOpc-CheapShark/YAIOpc.exe. Keep the full folder together. Close the current app before launching this version. Windows startup was not repointed.

Merged the archive's Library/Discover tabs, live CheapShark sales, title search, refresh, prices/discounts, store redirect links, and optional encrypted IGDB settings into the maintained source. Preserved ThemePalette integration and all other current app files. Deal grids resize with the page. Prices show USD; CheapShark scores are attributed to Metacritic. Updated the optional IGDB query to game_type and rating_count.

Build succeeded with the existing SteamArtwork obsolete API warning. Isolated --discovery-test passed 11 checks, including live deals/title search, price parsing, redirect URLs, rating attribution, tab switching, and three layout widths. Reports and rendered layouts: outputs/CheapShark-Validation. IGDB enrichment was not tested with credentials. Full combined UI suite was not rerun; the previously reported global spin hotkey conflict is unresolved.

Source additions: FramePilot/src/GameDiscoveryCatalog.cs. UI: FramePilot/wpf/GameLibraryPage.cs. CLI validation: Host/Program.cs --discovery-test <absolute-report-folder>.

All changes remain local; no commit or push was performed.