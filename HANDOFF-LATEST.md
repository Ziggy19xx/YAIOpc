# Confirmed True Stretch method — October 15, 2026

User confirmed this exact order produces both stretch and no black bars:
1. VALORANT: Windowed mode.
2. Switch to Fullscreen and Fill; apply/confirm.
3. Switch back to Windowed and Fill; apply/confirm.
4. Then apply the custom stretch resolution in YAIOpc.

Fill alone removed the bars but lost the desired stretch. Do not treat no bars alone as success. This supersedes older Windowed Fullscreen guidance for this user's setup. Keep this recipe visible in True Stretch.

Added Apply + fit VALORANT: switches the chosen resolution then performs the existing borderless window fit. It does NOT execute VALORANT's internal menu transitions. Those transitions remain unautomated: Windows game UI input failed with SendInput error 87 during live testing; no working settings-control API was established. Do not claim full one-click engine setup. User confirmed the combined Apply + fit VALORANT action works perfectly on October 15, 2026, preserving stretch and removing the black bars. This validates their setup; it does not establish automated internal game-menu transitions. No game configs, Windows monitor devices or live resolutions were changed by this update.

Latest build: outputs/YAIOpc-TrueStretch-Workflow/YAIOpc.exe. All changes local/unpushed.
# FPS overlay customization — October 13, 2026

Latest portable build: outputs/YAIOpc-FPS-Customizable/YAIOpc.exe. FPS Counter > Overlay now includes Show background and a 50–200% size slider. Both apply immediately and persist with existing overlay preferences. Background off removes the panel and border, retaining a subtle text shadow. Scaling resizes the whole overlay and keeps corner positioning. Existing saved settings default to background on and 100%.

Build passed with the existing SteamArtwork warning. Layout review passed all 56 page/width combinations with zero overflow candidates; FPS controls visually inspected at 1060 width. Reports: outputs/FPS-Customizable-Layout-Final. Numeric FPS capture was not retested by this appearance change and remains unverified as documented below. All changes local/unpushed.
# Always-visible system-wide FPS overlay — October 13, 2026

Latest build: outputs/YAIOpc-FPS-AlwaysOn/YAIOpc.exe. Build passed with the existing SteamArtwork warning. All source changes local/unpushed; startup target unchanged.

Changed FPS Counter to one on/off checkbox; removed process picker/manual Java/game selection. Capture runs across processes. Readings automatically resolve the foreground application's process family, including browser renderer/GPU children, without summing unrelated frame rates. Overlay remains visible while enabled even without readings, showing an explicit waiting state; it follows the selected screen corner and remains click-through. No fabricated refresh-rate/FPS fallback. Restored standard PresentMon display/GPU tracking instead of reduced tracking flags; that change alone has not established the missing-frame root cause.

Live computer-use check: previous YAIOpc-FPS app was stopped and closed normally. YAIOpc-FPS-AllApps was opened, its counter enabled, and Minecraft's existing session activated. The overlay was visibly present at the top left during gameplay, labeled javaw / Waiting for frames. Actual numeric Java FPS was NOT verified. Standard-privilege helper diagnostics show zero CSV rows from PresentMon. Earlier standalone timed captures had Java rows, but the continuous helper currently does not. Do not claim all games work or that numeric FPS is fixed.

Administrator capture retry was invoked in the running YAIOpc-FPS-AllApps app. Windows approval is pending; the assistant cannot approve UAC. User was asked asynchronously to approve or decline. The running build starts its 25s pipe timeout before UAC approval, so a delayed approval may time out. The newer YAIOpc-FPS-AlwaysOn source fixes this: administrator launch occurs off the UI thread, and the connection timeout starts after launch returns. It also uses a local lifetime/generation so Stop cancels startup safely. If the pending older request expires, use the latest build for the retry after the user resolves the old UAC prompt.

Validation: expanded parser/process-family test suite passes synthetic cases up through helper connection, but the live WPF ETW assertion failed with standard capture; full suite is NOT passing. Previous 17-pass report applies to older capture flags and does not prove this build works. Current reports: outputs/FPS-AllApps-Validation/fps-tests.txt; outputs/FPS-Stream-Diagnostic/stream-diagnostic.txt. Layout review: outputs/FPS-AlwaysOn-Layout. Native UI screenshots were inspected through computer-use; don't claim an exported screenshot artifact unless one exists.

Next required work: finish elevated capture test after human approves Windows; verify a numeric reading in the actual Minecraft game, then a browser/video and automatic transitions. Remove temporary diagnostics if no longer useful; keep reliable support diagnostics if appropriate. Do not modify game profiles or Windows permissions to work around this without a specific need.
# FPS Counter — October 13, 2026

Latest complete portable build: outputs/YAIOpc-FPS/YAIOpc.exe. Keep the entire folder together. Close the previous app before launching. Startup target was not repointed.

Open PC performance > FPS Counter. Click Start counter, then focus a game. Automatic follows the foreground application; Refresh running games allows pinning a PID. Displays average presented FPS and frame time over the latest second plus 1% low from up to five seconds of bounded history. Select overlay corner; Ctrl+Shift+F10 toggles overlay visibility during capture. Capture continues while other pages are open or the main app is minimized, and stops on explicit Stop or app close. Capture never autostarts. Overlay preferences persist; process IDs do not persist across launches.

Uses bundled official PresentMon 2.3.1 x64 through ETW. SHA256 364e5d98d4d134bd54dd25c22ed2ca2f4883f8bc3ed6502bee0c151e3436d30c was verified against GitHub release metadata. Licenses/provenance are in fps/. The 2.6.0 build detected events but emitted no usable CSV on this host, so the tested 2.3.1 release is pinned. It supports DirectX, Vulkan and OpenGL capture, but universal game/anti-cheat compatibility is not claimed. External overlays may be covered by exclusive fullscreen; use windowed/borderless or read FPS inside the app.

If Windows denies ETW access: Stop, then Start with admin access. Only the helper requests Windows elevation; the app and game are not restarted. That UAC path was implemented but not interactively approved/tested. The helper and app communicate over a random current-user-only named pipe. A per-helper job owns the capture engine; stop/disconnect terminates only its uniquely named trace session. No game injection, driver installation or persistent permission changes are performed. Stale data clears after 2.5 seconds; different process/swapchain streams are not combined.

Validation: build succeeded with existing SteamArtwork warning. --fps-test exited 0: 17 checks, including quoted/case-insensitive CSV schema, known FPS/frame-time/lows, dominant swapchain selection, PID isolation, invalid/stale data, bounded history, actual ETW frames from a native animated WPF window, overlay click-through/non-activation/focus, hiding and helper cleanup. This validates real frame capture, not every game or anti-cheat. Reports and native overlay preview: outputs/FPS-Validation.

--layout-review exited 0 across 28 pages at 1060x900 and 1480x900; no horizontal overflow candidates, including the FPS page. Reports/screenshots: outputs/FPS-LayoutValidation. Existing full combined smoke was not rerun.

Implementation: Host/FpsCapture.cs, Host/FpsPage.cs, Host/FpsTests.cs; navigation Host/Shell.cs; worker/test dispatch Host/Program.cs; packaging Build.ps1. All changes local and unpushed.
# Responsive layout fixes — October 13, 2026

Latest full portable build: outputs/YAIOpc-LayoutFix/YAIOpc.exe. Keep the complete folder together. Close the current app before launching. Startup target was not repointed.

Fixed clipped Home CPU/GPU/RAM/disk labels with constrained icon/text grids and responsive two/four-card layouts. Home chart controls sit below the title; chart notes wrap. Quick Actions reflow to readable rows. Lower Home cards stack when narrow. Game Library hero, statistics, search/filter toolbar and sort controls reflow; descriptive text wraps. Audio Studio statistics and routing/shaping columns reflow. Driver/app inventory columns fit available width with horizontal scrolling available; PC Monitor table columns resize to retain Unit and Availability.

Build succeeded with the existing SteamArtwork obsolete API warning. --layout-review rendered 27 main routes at actual offscreen sizes 1060x900 and 1480x900 (54 page/width combinations), plus middle and bottom scroll captures. The final bounds scan found zero horizontal overflow candidates in untrimmed visible text. Home, Game Library, Audio Studio and monitor screenshots were visually inspected. This is page-layout coverage, not proof that every dialog, live-data string or DPI combination is perfect. Existing intentional ellipsis for long data remains.

Final reports and screenshots: outputs/LayoutFix-Review-Final. --library-navigation-test exited 0 with 10 passing checks. No tuning, audio or game actions were applied by the layout review. Full combined functional smoke was not rerun.

Implementation: Host/ResponsiveLayout.cs, Host/LayoutReview.cs; Home XAML/WpfShell, GameLibraryPage, MonitorPage, ToolPages and VelvetVoice/Wpf/StudioWindow. All changes remain local and unpushed.
# PC Monitor styling and checkbox fix — October 13, 2026

Latest full portable build: outputs/YAIOpc-MonitorStyle/YAIOpc.exe. Keep the entire folder together. Close the previous app before launching. Startup target was not changed.

Fixed startup checkbox by using a centered vector tick and omitting active-frame ornament pixels on small controls. PC Monitor now uses shared theme brushes, a teal/gold heading, CPU/memory/GPU metric cards, labeled chart grid with empty-state guidance, grouped controls, and themed hardware table. The page uses one main scrollbar instead of a separately scrolling header. Existing polling, inventory, filters, export and optional sensor controls remain connected to the same model.

Build passed with the existing SteamArtwork warning. Navigation validation exited 0 with 10 passing checks; monitor layouts at 1060/1280/1600 and checked startup checkbox were rendered. The 1060 layout was visually inspected. Reports/previews: outputs/MonitorStyle-Validation.

All changes remain local and unpushed.
# Cyber Gold reference styling — October 13, 2026

Latest full portable build: outputs/YAIOpc-CyberGold/YAIOpc.exe. Keep the complete folder together. Close the current app before launching. Startup target was not repointed.

Sidebar styled from the supplied image: native angular frames, subtle teal scan lines, cyan icons and chevrons, gold active item, Consolas labels, framed PC logo, matching startup checkbox and footer. Retains Home, Game Library, PC performance, Audio, Customization and More tools navigation. The Cyber Gold palette applies across the app; the first launch selects it once and records a marker beside theme.json. Later theme choices remain persistent. Sidebar chrome retains the reference's teal/gold styling.

Build passed with the existing SteamArtwork warning. --library-navigation-test passed 10 checks and exited 0, covering sidebar order, independent selection, dashboard shortcuts and 1060/1280/1600 layouts. Rendered Home and Game Library previews were inspected. Reports: outputs/CyberGold-Validation. Full combined smoke and live hardware/audio were not rerun.

Native styling implementation: CombinedStudio-source/Host/CyberSidebar.cs; integration: Host/Shell.cs; palette: Host/AppThemes.cs. All changes remain local and unpushed.
# Game Library sidebar — October 13, 2026

Latest full portable build: outputs/YAIOpc-LibrarySidebar/YAIOpc.exe. Game Library is now a standalone sidebar tab between Home and PC performance. Home no longer contains the Home/Game Library tab strip. Dashboard game links still open Game Library. CheapShark Discover remains inside Game Library. Startup target was not repointed. All changes remain local and unpushed.
# CheapShark game discovery — October 13, 2026

Latest complete portable build: outputs/YAIOpc-CheapShark/YAIOpc.exe. Keep the full folder together. Close the current app before launching this version. Windows startup was not repointed.

Merged the archive's Library/Discover tabs, live CheapShark sales, title search, refresh, prices/discounts, store redirect links, and optional encrypted IGDB settings into the maintained source. Preserved ThemePalette integration and all other current app files. Deal grids resize with the page. Prices show USD; CheapShark scores are attributed to Metacritic. Updated the optional IGDB query to game_type and rating_count.

Build succeeded with the existing SteamArtwork obsolete API warning. Isolated --discovery-test passed 11 checks, including live deals/title search, price parsing, redirect URLs, rating attribution, tab switching, and three layout widths. Reports and rendered layouts: outputs/CheapShark-Validation. IGDB enrichment was not tested with credentials. Full combined UI suite was not rerun; the previously reported global spin hotkey conflict is unresolved.

Source additions: FramePilot/src/GameDiscoveryCatalog.cs. UI: FramePilot/wpf/GameLibraryPage.cs. CLI validation: Host/Program.cs --discovery-test <absolute-report-folder>.

All changes remain local; no commit or push was performed.
---
# YAIOpc continuation handoff - October 13, 2026

This supersedes older build pointers in HANDOFF.md. Latest full portable build: `outputs/YAIOpc-MonitorFit/YAIOpc.exe`. Close the current app first; keep the entire folder together. Project: `CombinedStudio-source/CombinedStudio.csproj`. All work remains local and uncommitted; no push was performed. Preserve unrelated changes already present.

## Completed

- Crosshair: four aligned passes at reduced resolutions and one-physical-pixel minimum for built-in lines/dots. Thickness and gap controls beside size/stretch support built-in and community model lines. Community overrides are nullable and preserve originals; PNG layers retain original pixels.
- Crosshair appearance: per-design hue, Solid, horizontal/vertical/radial gradients, Stripes, Checker, Dots, strength and reset. Applies only to the crosshair drawing. Settings save with presets. Neutral hue-only pixels and dark outlines retain original color.
- Fixed appearance changing shape: viewport-aligned final rasterization for both plain and styled designs; RGB changes preserve exact alpha. Do not reintroduce cropped fractional-origin rasterization or apply another scale after coloring. 237 crosshair checks passed, including exact masks for stretched/rotated/low-resolution/fitted/community/PNG cases. Report: `outputs/Crosshair-ShapeFix-Validation/crosshair-tests.txt`.
- Logo: gold/graphite PC tower with a Y integrated into the front panel. `Host/Assets/yaiopc-logo.png` and multi-size `yaiopc.ico` are embedded; executable, window and sidebar use them. Original hub mark saved as `yaiopc-logo-hub-v1.png`. Built-in imagegen prompts are in `Host/Assets/YAIOPC-LOGO.md`. Transparency and icon decode verified; build passed. Previous sidebar screenshot verified integration. Full UI smoke stopped at an existing global spin hotkey registration conflict; don't claim it passed.
- Window fitting: `Host/WindowFit.cs` opens Normal/windowed on monitor under pointer, clamps preferred 1480x1020 DIP size and minimums to work area, accounts for DPI/taskbar, centers and refits on display/DPI change. Fixture sessions skip startup sizing. `--window-fit-test <folder>` passed 21 checks, including a real native WPF window fitting current work area. Report: `outputs/MonitorFit-Validation/window-fit-tests.txt`.
- True Stretch: borderless maximization instead of WS_POPUP; verifies full-display client dimensions and restores original window. 26 stretch checks passed including native test window. Live VALORANT removal of embedded black bars remains unverified. Tools found no game process when user reported practice range running. Current Fit command requires Windowed, not Windowed Fullscreen. Do not claim the game-rendered bars are solved.

## Build

Private SDK: `C:\Users\Ziggy\Documents\Codex\2026-10-03\hello-hello-can-you-hear-me\work\dotnet\dotnet.exe`.

Use `CombinedStudio-source/Build.ps1 -DotNet <SDK-path> -OutputDirectory <new-output-folder>`. Existing SteamArtwork obsolete API warning remains. Run WinExe tests with `Start-Process -WindowStyle Hidden -Wait -PassThru` and inspect ExitCode. Publish to a new full folder; don't stop the user's app or replace locked binaries. Latest build includes previous crosshair fixes and PC logo. Startup shortcut/registry target was not repointed in this continuation.

## Other context

Minecraft diagnosis only; no profile files modified. Profile: `C:\Users\Ziggy\AppData\Roaming\ModrinthApp\profiles\Fabric 1.21.1`. Physics Mod Pro access-widens `class_761.method_3295` and conflicts with VulkanMod's private overwrite. Subsequent native crash `hs_err_pid27252.log` shows Essential overlay making OpenGL calls with VulkanMod active. Suggested disable Physics Mod Pro and Essential to keep VulkanMod, or VulkanMod+Beryl to keep them. Resource-pack issues: corrupt Bare Bones PBR x Fresh Animations ZIP, missing pack_format in LOW_ConnectedTextures_RealisticJAVApack3.3, unsupported axe-model rotation in Enchantment Outlines. Later menu initialization did not prove stability; native crash was discovered afterward.

User's VALORANT ban appeal request was transcription only. No sending or account action authorized/performed.

Monitor suite exited 0 with 29 passing checks when run with normal hardware access, including real samples, worker lifecycle and isolated storage fixture.
