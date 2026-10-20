# Changelog

## Unreleased

- **Every section can change column.** The 14 sections that host an add-on control (Play Notes, News, Video, GameActivity, CheckDlc, Game Relations, Web Explorer, HowLongToBeat, Achievements, Steam Screenshots, VNDB, ScreenshotsVisualizer, Review Viewer, Screenshot Utilities) used to be fixed in their column. They cannot be duplicated (an add-on fills its control by name, once), so the extension now moves the live block between the two columns, from the profile. Nothing changes until you move one; without the extension the theme keeps its own columns.
- Sections page: an amber **drop line** shows where the section will land, a section that cannot go to a column gets the "no drop" cursor, every block has a **left/right button** (one undo step), and the empty area under a column accepts a drop at its end.
- Fixed: dropping a section lower in its own column landed one place too far.
- Fixed: the **Corners** buttons (Sharp / Soft / Designed / Pill) did nothing, and the "All corners" slider drifted or stuck while dragged (it read back a different value than it wrote). Sliders in general now hand back exactly what they wrote while the stored value is its snap, so a slow drag of less than half a step moves too.
- New **Items** page: one foldable card per piece of information or section (Genres, Release date, HowLongToBeat, Achievements...) with every option for it (show in the chips, new line, section open by default, column, tab, grid view, game list, covers). A section block of the Sections page opens its card with its **...** button.
- An Items card tells you when an item is shown twice on the game page (as a chip and as its own section) and folds the section at the bottom in one click.
- **Information shown once**: the Items page lists the items shown on several places (artwork chips, header tile, their own section, the information panel), lets you keep each on one of them (or force the exception) and hides the other copies in one click, or for all items at once.
- The **Header cards** page now holds every header parameter: the cells, a table of the information chips (shown / new line before) and all the other header settings.
- Color constants and the custom accent have a color picker (swatches and sliders).
- The editor can switch to the **Fullscreen** theme (Desktop / Fullscreen buttons) and edit its profile from the desktop.
- Fixed: a locked shared look file no longer keeps the profile from being saved.
- Margins, paddings and corner radii are edited as four sides (left, top, right, bottom; the four corners) with a small picture that follows the numbers, instead of text like `12,7,12,7`.
- Preset tiles show only the part of the picture that differs between the choices, and a folded list of the settings each choice sets. Sliders of the artwork card look, opacities and font sizes carry a small schematic that follows the slider.
- The "Figure colors" page is now **Thresholds**; new **Diagnostics** page (copy for a bug report); **profile switcher** on the Profile page; Undo / Redo say what they change; a reset of six settings or more asks for a second click; the "Put back as before" button for Playnite's own settings shows again after a restart.
- Sizes page: a preview dock says whether the selected size is on screen now (view and selected game) and draws it when it is not.
- A shared **Look** (`look.profile.json`): accent, corners, glass and motion are chosen once and drive both the desktop and the Fullscreen theme. The Fullscreen radii, glass and the Accent / Corners / Glass / Window darkness presets are generated from the desktop ones (`tools/sync_tokens.py`, checked by `tools/check-sync.py`: 18 tokens in sync, 7 partial, 4 to do).
- Performance review (`docs/reviews/perf-2026-10-20.md`): the section mover costs about 5 ms per session and was left alone; profile writes were already debounced. Measurement tools for the testbed (`tools/perf-*.ps1`).
- Developer tools: in-process screenshots and key injection for the testbed (`ShotService`, `tools/fs-shot.ps1`, `tools/fs-keys.ps1`), which also work while the monitor sleeps.
- Build and test output (`build`, `testbed`, `backup`) can live outside the repository: set `MODULA_LOCAL` to a folder (default: the repository root, as before).

## 0.2.0

- Presets are shown as picture tiles; short lists of choices are a segmented bar.
- Number boxes have no upper limit (the slider range is only a guide; the lower bound stays so a width cannot go negative).
- The settings pages no longer repeat what a visual page edits (sections, sizes, corners, background, header cards), group titles are shorter.
- New pages: Game header, Game page, Text and links, Glass and popups, Extensions.
- New first page **Graphics and performance**: checks Playnite's window-background options and a few theme choices, says whether each touches graphics, performance or both (with measured numbers), and fixes the Playnite ones in one click.
- The advice no longer calls anything a problem without a measurement: turning off "show the background image on the window" is now reported as the problem (it empties the window background), and options that are off apply nothing.
- The advice page, the banner and the start notification are available in French (the English in the code is the fallback).
- One notification at start names the optional add-ons that are not installed.
- The editor window is taller so the sidebar fits without scrolling.
- The theme's gear buttons open the editor on a given page (`OpenEditor` takes a page id).

## 0.1.0

First public release.
