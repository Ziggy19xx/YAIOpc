# Themes

Open **Customization → Themes** and select **Apply theme** on a preview card.

Eight palettes are available: Copper (original), Midnight, Ocean, Amethyst, Forest, Rose, Sunset and High Contrast. Selection applies immediately to shared navigation, page surfaces, cards, interface text, borders and accent colors in the performance, audio and customization workspaces. Existing controls update in place; restarting is not required. The original Copper palette can be restored from the same page.

Crosshair geometry/colors, artwork, status colors, audio settings and display settings keep their own values. This changes the application interface only.

Preference: `%LOCALAPPDATA%\YAIOpc\Appearance\theme.json`. The selected theme loads on startup. Missing, unknown or unreadable preferences fall back to Copper; a read error is shown on the Themes page. No administrator permission is needed.

Latest portable build: `outputs/YAIOpc-Themes/YAIOpc.exe`. Keep the entire folder together. Existing settings remain compatible.

Implementation uses dynamic WPF color resources and bound shared palette brushes, so cached pages and templates can switch themes without freezing the active colors. Theme preview samples use independent brushes and keep showing their own palette.
