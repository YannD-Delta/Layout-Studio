# Changelog

## Layout Studio 0.3.0, 2026-10-21

- **Global presets** (30), a generated backdrop, section header and information panel switches, the new colour profiles and fonts, and the fixed grid view: see the Modula changelog. The plugin now edits **705 settings** (646 Desktop, 59 Fullscreen).
- **Screenshots in one command**: `tools/refresh-screenshots.ps1` redoes every picture (desktop, Fullscreen, editor pages, preset previews) at 1080p from a manifest, with a blank-picture check; `tools/scene-shot.ps1` drives Playnite through the views (list, grid, grid + details, explorer, filters) in-process.
- **Add-ons page**: four new blocks (ImageRotater, Now Playing and Back To Game, AnikiHelper, PlayerActivities); each block says whether its add-on is installed and what it adds to the theme.
- The Sections page and the Items page know the new **Player Activities** section (show, open, column, order, header, tab).
- Recommended add-ons: ImageRotater and Now Playing join the start notification list (Desktop only; never required).
- Developer tools: `tools/testbed-ext.ps1` installs an add-on from its own GitHub release into the test bench only (zip-slip check, install log) and removes it again; `tools/ext-shot.ps1` captures the bench with a few option overrides.
- **Generated backdrop** block on the Window page: a mode chooser (game artwork, one colour, gradient, radial, circles), a **Randomize** button (new seed and palette, one undo step) with the harmony (analogous, complementary, mono, warm, cold, pastel) and the kind of circles (aurora, bokeh, mixed), a palette built on the **accent** or on the **dominant colour of the game artwork**, a seed box (same seed, harmony and style give the same backdrop), the colours and sliders of the backdrop, a pattern and the table of the eight circles behind "More", and thirteen ready-made looks with a picture. The real window changes as you edit; hovering the block lights the window of the plan. `BackdropGenerator` (Core) is pure and unit-tested: deterministic, harmonious palettes, circles spread over the window.
- **Global presets**: a new first page, "Global preset", shows one card per preset with a picture of the real window (captured on the test bench). One click asks, a second applies it; the profile you had is saved first. A preset sets every preset group and every value; the shared groups (Glass, Accent, Corners, Window darkness) are written to the shared Look, so Fullscreen follows. A new profile starts from the default preset and "Reset all" (and the quick Reset menu entry) returns to it; resetting one setting, one block or one page still goes back to the theme's own default.
- The Items page lists the 17 rows of the information panel (each with its own show switch), the Section look block has the section header switches (global and per section).
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
- The **Background** page shows the real window (the game's own artwork through the real glass layers, scrims and panels) instead of a drawing, and follows the sliders.
- **New navigation of the editor**: ten pages in two levels (page, then headed blocks): Window and panels, Game page, Sections, Items, Colors and text, Sizes corners and density, Grid view, Add-ons, Performance, Profile. Every one of the 532 options has exactly one home (a data file, with a test that fails on an orphan or a duplicate), each page starts with a small plan of the window lighting the area it controls, and there are a search that opens the page and highlights the setting, an "only changed" filter, a reset per block and one "More" link per block. The Add-ons page lists every supported add-on with its state, what it gives the theme and links to the places where it shows.
- **Items page rebuilt**: one flat page of lists by zone (chips, cards, buttons and widgets, information panel, sections, grid/list/covers), every item generated from the theme options with all its places, with an order for the chips and for the header cards (drag handle and up/down arrows, applied live).
- The Sections page buttons are drawn as vector icons (they were clipped), and hovering any setting shows one plan of the window with the part it touches lit; the Background page no longer has its own preview and thumbnails.
- Typing a number in the box next to a slider and pressing Enter now applies it; Undo and Redo keep a fixed width; the page content is capped at 1100 px (4K).
- Profiles are version 2: the section orders move down by one once (the Links section took row 0), chip orders are migrated.
- The completion status picker of the header card (Playnite's own statuses, written back to the game).
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
