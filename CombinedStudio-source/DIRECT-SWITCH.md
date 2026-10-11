# Direct UFO switch

`FramePilot.exe --ufo-switch` opens an opt-in experiment. `Open UFO Switch.cmd` launches it from the published folder. It does not alter the default full studio launch or the previous desktop shortcut.

Focus the real TestUFO browser window and press F8. The tool places a black, click-through, non-activating layered mask over the foreground window's client area. One horizontal band remains visible per mask phase; the other bands are black. No capture or scaling occurs. The source continues updating underneath. This differs from the earlier captured-video pipeline; neither controls the monitor's backlight.

F8 toggles off. Ctrl+Shift+F8 is an emergency off hotkey. Focus loss, target closure, minimization, moving/resizing the window, compositor failure, or 30 seconds of testing stops the mask. Closing the controller also stops it. Hotkey conflicts are reported before testing. Pulse bands range from 2 to 8 and default to 4. Four bands imply roughly 25% visible duty in the ideal timing model.

Mask updates use Win32 window regions and DwmFlush, with sampled loop cadence displayed in the controller. Cadence is not a measurement of physical scanout, panel persistence, input latency or delivered refresh-by-refresh mask accuracy. Desktop composition may repeat, skip or delay mask changes. This is not verified DyAc, a hardware strobe, or an AI processing model. No competitive advantage is established. Flicker, darkening, tearing or banding may make it worse.

Only use on a test browser window initially. The overlay covers the entire selected window client area, including browser UI; it does not identify the UFO canvas. If TestUFO is in the Codex window, it masks that entire window. A separate browser window is preferable. There is no game injection, memory access, anti-cheat interaction, driver change, startup entry or permanent display setting change.

Controller smoke validation checks default-off startup and hotkey registration without activating the physical mask. Physical presentation and visual benefit remain unverified until the user toggles it and compares. No game latency measurement is performed.

Gentler comparison: select 2 bands and 75% mask strength while OFF, then focus TestUFO and press F8. Two bands revisit each screen location twice as often as four at the same delivered cadence. Strength adjusts layered-window opacity from 25% to 100%; lower strength lets the source remain visible during masked phases, potentially reducing flicker and also reducing clarity benefit. This does not increase monitor refresh rate. Defaults preserve the original four-band opaque mask.
