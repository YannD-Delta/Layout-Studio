# Changelog

## Unreleased

- **Every section can change column.** The 14 sections that host an add-on control (Play Notes, News, Video, GameActivity, CheckDlc, Game Relations, Web Explorer, HowLongToBeat, Achievements, Steam Screenshots, VNDB, ScreenshotsVisualizer, Review Viewer, Screenshot Utilities) used to be fixed in their column. They cannot be duplicated (an add-on fills its control by name, once), so the extension now moves the live block between the two columns, from the profile. Nothing changes until you move one; without the extension the theme keeps its own columns.
- Sections page: an amber **drop line** shows where the section will land, a section that cannot go to a column gets the "no drop" cursor, every block has a **left/right button** (one undo step), and the empty area under a column accepts a drop at its end.
- Fixed: dropping a section lower in its own column landed one place too far.
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
