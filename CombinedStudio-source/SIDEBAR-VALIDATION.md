# Sidebar and Velvet Audio branding validation — October 10, 2026

Latest release portable build: `outputs/FramePilot-VelvetAudio-Unified/FramePilot.exe`. Build succeeds with the existing SteamArtwork obsolete API warning.

Combined UI suite: exit 0, 71 passing checks. The two previous rails are replaced with one 276-pixel sidebar. FramePilot and Velvet Audio expand/collapse independently; expanding either restores that application's selected page. More tools starts closed underneath both applications, shows/hides secondary items, and opens when a secondary page is selected. Shared navigation marks one active page. Existing routes, EQ bindings, retained settings, synthetic audio continuity, modal dialogs, and 1060/1280/1600 pixel layouts pass. Rendered expanded/collapsed screenshots were visually inspected. Sidebar width and workspace position stay fixed when switching applications.

Offline audio suite: exit 0, 55 passing groups. Existing settings folders, internal namespaces, and the original APO ownership marker/file names are retained for compatibility; user-facing names and dialogs use Velvet Audio.

Reports: `outputs/Unified-sidebar-validation/combined-tests.txt` and `outputs/FramePilot-VelvetAudio-Unified/test-results.txt`. Tests used isolated settings and temporary APO fixtures and did not enable live microphone or headphone processing.
