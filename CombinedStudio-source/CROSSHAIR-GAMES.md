# Community game crosshairs

The October 11, 2026 public Crosshair X / mod.io catalog snapshot contains:

| Game | Uploads |
| --- | ---: |
| Valorant | 1,154 |
| Fortnite | 2,282 |
| Counter-Strike (CS:GO / CS2 category) | 2,573 |

There are 5,980 unique uploads. Some creators tag an upload for several games. All three game sections and the combined community category participate in the randomizer; All designs combines the community index, original geometric designs, and saved presets. Generated designs retains the existing procedural generator.

Find the galleries under Customization → Crosshair Studio → Browse Valorant, Fortnite and Counter-Strike. Search names or creators, sort by rating/name/creator, and browse 24 entries per page. Each entry links to its creator's source page. Downloaded designs are cached alongside crosshair settings; save a selected design as a favorite to retain its source reference across restarts.

The entire catalog index is bundled. Actual packages download on demand, including randomizer candidates; uncached designs require Internet access. Four simultaneous transfers, cancellation, atomic cache writes, and compressed/uncompressed size limits protect the import path. ZIP contents are read in memory without extracting paths or executing code. No account credentials or API keys are collected.

Compatibility: resting geometric and embedded image layers render from the original package. GIFs use their first frame. Crosshair X firing/recoil/ammo animations and scripts are not reproduced. Unknown layer formats, multiple-design packages, oversized assets, missing or removed downloads, and invisible resting designs can fail to load; the existing crosshair is preserved and an error is shown. Catalog membership does not imply that every upload has been individually verified.

Sources: https://mod.io/g/crosshair-x/m and its game-tag filters. Package fixtures are real public uploads: Fortnite Pump Crosshair (6443835, paulos4) pumpv2 (6383250, tonyscott15), and Center O (6166880, lurk27). Their original model/image data is used to validate the importer. Creator attribution and source URLs are retained in the bundled index.

Validation commands:

```
YAIOpc.exe --crosshair-test <isolated-output-folder>
YAIOpc.exe --crosshair-community-test <isolated-output-folder>
YAIOpc.exe --combined-smoke <isolated-output-folder>
```

The community check downloads and renders four top-rated entries from each game in an isolated cache. The general crosshair check covers catalog coverage, game filtering, immutable randomizer choices, fixture rendering, source-reference persistence, import validation and existing procedural behavior. UI smoke checks exercise pagination/search and gallery layouts at 1060, 1280 and 1600 pixels, alongside existing app routes.

Final validation: 101 crosshair checks, 12 public package download/render/randomizer checks, and 101 combined UI smoke checks passed. No game-tag mismatches or missing download file IDs were found in the bundled index. The build has one existing SteamArtwork obsolete-API warning.

Preview overflow fix: every CrosshairView clips drawing to its own bounds. Gallery and carousel previews shrink oversized drawings to fit with padding, including rotated and offset image layers; small crosshairs retain their normal size. Overlay and PNG export rendering keep their actual size. Verified with 107 crosshair checks (including oversized-image containment) and 101 combined UI checks, plus screenshots using the same cached anime and Alya designs reported by the user.
