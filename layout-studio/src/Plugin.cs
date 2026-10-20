using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using LayoutStudio.Core;
using LayoutStudio.Editor;
using LayoutStudio.Engine;

namespace LayoutStudio
{
    /// <summary>Settings object Playnite hands to {PluginSettings}: only <see cref="Options"/> is read by the theme.</summary>
    public class ExposedSettings : ObservableObject, ISettings
    {
        private OptionsBridge options;
        public FactsViewModel Facts { get; set; }

        /// <summary>Screenshots and trailer of the selected game for the Fullscreen details page (Path=Media.Screenshots, Media.HasScreenshots).</summary>
        public MediaViewModel Media { get; } = new MediaViewModel();

        /// <summary>Opens the Layout Studio editor; the theme's settings button at the top binds its Command to this.</summary>
        public System.Windows.Input.ICommand OpenEditor { get; set; }

        public OptionsBridge Options
        {
            get { return options; }
            set { options = value; OnPropertyChanged(); }
        }

        public void BeginEdit() { }
        public void CancelEdit() { }
        public void EndEdit() { }
        public bool VerifySettings(out List<string> errors) { errors = new List<string>(); return true; }
    }

    /// <summary>Entry point. Named LayoutStudioPlugin because Playnite.SDK.Plugins.Plugin is the SDK base type.</summary>
    public class LayoutStudioPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private StudioService studio;
        private SectionMover sectionMover;
        private Window editor;
        private ShellViewModel shellModel;
        private readonly ExposedSettings exposed = new ExposedSettings();

        public override Guid Id { get; } = Guid.Parse("8757c9cb-201d-460c-8735-ffc39a617269");

        public LayoutStudioPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = false };
            AddSettingsSupport(new AddSettingsSupportArgs { SourceName = "LayoutStudio", SettingsRoot = "Settings" });
            // Loaded as early as the constructor so overrides are in place before the theme's views are parsed
            // (constants read through {StaticResource} are resolved once, at parse time).
            try
            {
                // Playnite loads extensions in both applications: each mode only ever touches its own theme, with its own profiles.
                bool isDesktop = api.ApplicationInfo.Mode == ApplicationMode.Desktop;
                string themeId = isDesktop ? api.ApplicationSettings.DesktopTheme : api.ApplicationSettings.FullscreenTheme;
                var enabled = api.Addons.Addons.Except(api.Addons.DisabledAddons ?? new List<string>()).ToList();
                var guard = ThemeGuard.Evaluate(themeId, enabled, isDesktop);
                if (guard == GuardResult.WrongTheme) { logger.Info("LayoutStudio idle: the active " + ThemeGuard.ModeFolder(isDesktop) + " theme is not Modula"); return; }
                if (guard == GuardResult.ThemeOptionsActive) { logger.Warn("LayoutStudio idle: ThemeOptions is enabled; disable it first so settings are not applied twice"); return; }
                string themeDir = Path.Combine(api.Paths.ConfigurationPath, "Themes", ThemeGuard.ModeFolder(isDesktop), themeId);
                string dataDir = isDesktop ? GetPluginUserDataPath() : Path.Combine(GetPluginUserDataPath(), "fullscreen");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                studio = StudioService.Create(themeDir, dataDir, Application.Current.Dispatcher, Path.Combine(GetPluginUserDataPath(), "look.profile.json"));
                long createMs = clock.ElapsedMilliseconds;
                Action<string> log = line => logger.Warn(line);
                studio.Log = log;
                studio.Applier.Log = log;
                studio.ProfileApplier.Log = log;
                foreach (var problem in studio.SchemaProblems) logger.Warn("LayoutStudio schema: " + problem);
                if (studio.Look != null && studio.Look.Warning != null) logger.Warn(studio.Look.Warning);
                studio.LoadActive();
                long loadMs = clock.ElapsedMilliseconds - createMs;
                if (studio.Active.Warning != null) logger.Warn(studio.Active.Warning);
                exposed.Options = new OptionsBridge(studio.Store);
                exposed.Facts = new FactsViewModel(FactsSettings.From(studio.Active.Editor));
                // the visual editor is a desktop window: in the fullscreen app the quick menu (main menu, Layout Studio) is the way in
                if (isDesktop) exposed.OpenEditor = new LayoutStudio.Editor.RelayCommand((Action<object>)(page => OpenEditor(page as string)));
                if (isDesktop)
                {
                    // moves the sections that host plugin controls between the two columns, as the profile says (see Engine\SectionMover.cs)
                    sectionMover = new SectionMover(studio.Schema, studio.Store, line => logger.Info(line));
                    sectionMover.Start();
                }
                studio.ProfileReplaced += () => exposed.Facts.Reload(studio.Active.Editor);
                ShotService.StartIfRequested(Application.Current.Dispatcher, line => logger.Info(line));
                // the editor's Background page shows the real window (artwork, glass layers, panels) instead of a drawing of them
                if (isDesktop) LayoutStudio.Editor.BackgroundEditorViewModel.LiveCapture = () => LivePreview.Capture(640);
                logger.Info("LayoutStudio loaded profile '" + studio.Active.Name + "' (" + studio.Store.Changed().Count + " overrides, " + ThemeGuard.ModeFolder(isDesktop).ToLowerInvariant() + "), schema " + createMs + " ms, profile " + loadMs + " ms, total " + clock.ElapsedMilliseconds + " ms");
            }
            catch (Exception ex)
            {
                studio = null;
                logger.Error(ex, "LayoutStudio failed to initialize; the theme keeps its defaults");
            }
        }

        /// <summary>Root of {PluginSettings Plugin=LayoutStudio, Path=...}: the theme reads Settings.Options[Key].</summary>
        public ExposedSettings Settings { get { return exposed; } }

        private readonly FactsLoader factsLoader = new FactsLoader();
        private int selectionGeneration;

        private const string GameActivityId = "afbb1a0d-04a1-4d0c-9afa-c6e42ca855b4";

        /// <summary>Median FPS, CPU, GPU and RAM of the selected game, from GameActivity's logged samples.</summary>
        public override void OnGameSelected(OnGameSelectedEventArgs args)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { OnGameSelectedCore(args); }
            finally { PerfProbe.HandlerTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started; PerfProbe.HandlerCalls++; }
        }

        private void OnGameSelectedCore(OnGameSelectedEventArgs args)
        {
            try
            {
                if (studio == null || exposed.Facts == null) return;
                var selected = args.NewValue;
                UpdateMedia(selected);
                if (selected == null || selected.Count != 1) { exposed.Facts.Update(null); exposed.Facts.SetScores(null, null, null); return; }
                exposed.Facts.SetScores(selected[0].CriticScore, selected[0].CommunityScore, selected[0].UserScore);
                string path = Path.Combine(PlayniteApi.Paths.ExtensionsDataPath, GameActivityId, "GameActivity", selected[0].Id.ToString() + ".json");
                exposed.Facts.Update(null);   // the previous game's figures never sit next to this title
                // The file can be several megabytes: it is read off the UI thread, cached while unchanged, and an answer that arrives
                // after another game was selected is dropped.
                int generation = ++selectionGeneration;
                var since = exposed.Facts.Since;
                string gameId = selected[0].Id.ToString();
                Task.Run(() => factsLoader.Load(path, since)).ContinueWith(t =>
                {
                    if (t.Status != TaskStatus.RanToCompletion) { logger.Warn("could not read the activity data of game " + gameId + ": " + (t.Exception == null ? "" : t.Exception.GetBaseException().Message)); return; }
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (generation != selectionGeneration || exposed.Facts == null) return;
                        exposed.Facts.Update(t.Result);
                        logger.Debug("facts for game " + gameId + ": fps " + t.Result.Fps.Count + " samples, median " + t.Result.Fps.Median);
                    }));
                });
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "could not read the activity data of the selected game");
            }
        }

        private const string ScreenshotUtilitiesId = "485d682f-73e9-4d54-b16f-b8dd49e88f90";

        /// <summary>The folders Screenshot Utilities and Extra Metadata Loader fill: read directly, they expose no paths to a theme.</summary>
        private void UpdateMedia(List<Game> selected)
        {
            try
            {
                if (selected == null || selected.Count != 1) { exposed.Media.Update(null, null); return; }
                string id = selected[0].Id.ToString();
                exposed.Media.Update(
                    MediaFiles.Screenshots(Path.Combine(PlayniteApi.Paths.ExtensionsDataPath, ScreenshotUtilitiesId), id, 8),
                    MediaFiles.Video(PlayniteApi.Paths.ConfigurationPath, id));
            }
            catch (Exception ex) { logger.Warn(ex, "could not list the media of the selected game"); }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            if (studio != null) studio.TrySave();   // an edit made less than a second before quitting is not lost
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info("LayoutStudio started");
            if (studio != null) studio.ProfileApplier.Live = true;   // the window exists now: structural presets wait for a restart
            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop) OfferThemeOptionsImport();
            AnnounceRecommendations();
            // Testbed helper: a marker file next to the sample artwork opens the editor at start (never present on a real install).
            string sampleFlag = Path.Combine(PlayniteApi.Paths.ConfigurationPath, "add-sample-library.flag");
            if (File.Exists(sampleFlag) && SampleLibrary.Available(PlayniteApi.Paths.ConfigurationPath))
            {
                try { File.Delete(sampleFlag); SampleLibrary.Fill(PlayniteApi, logger); }
                catch (Exception ex) { logger.Warn(ex, "sample library failed"); }
            }
            // Testbed measurement (tools\perf-probe.ps1): scrolls the list and switches games by code, logs the UI thread time per step.
            string perfFlag = Path.Combine(PlayniteApi.Paths.ConfigurationPath, PerfProbe.FlagName);
            if (File.Exists(perfFlag) && PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
                PerfProbe.Arm(perfFlag, line => logger.Info(line));
            string marker = Path.Combine(PlayniteApi.Paths.ConfigurationPath, "open-editor.flag");
            if (studio != null && File.Exists(Path.Combine(PlayniteApi.Paths.ConfigurationPath, "sample-bg.jpg")) && File.Exists(marker))
            {
                // the flag may name a page (visual-corners, header...); editor-page.txt next to it is read every second afterwards, so one run can show many pages
                string first = null;
                try { first = File.ReadAllText(marker).Trim(); } catch (Exception) { }
                Application.Current.Dispatcher.BeginInvoke(new Action(() => OpenEditor(string.IsNullOrEmpty(first) ? null : first)));
                string pageFile = Path.Combine(PlayniteApi.Paths.ConfigurationPath, "editor-page.txt");
                string last = null;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        if (!File.Exists(pageFile)) return;
                        string wanted = File.ReadAllText(pageFile).Trim();
                        if (wanted == last) return;
                        last = wanted;
                        if (wanted.Length > 0) OpenEditor(wanted);
                    }
                    catch (Exception) { }
                };
                timer.Start();
            }
        }

        /// <summary>One notification (not a dialog) naming the optional add-ons that are not installed; shown again only when that list changes.</summary>
        private void AnnounceRecommendations()
        {
            try
            {
                if (studio == null) return;
                var missing = Recommendations.Missing(PlayniteApi.Addons.Addons, PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop);
                string text = Recommendations.Message(missing, string.Equals(PlayniteApi.ApplicationSettings.Language, "fr_FR", StringComparison.OrdinalIgnoreCase));
                if (text.Length == 0) return;
                string flag = Path.Combine(GetPluginUserDataPath(), "recommendations-announced.txt");
                string signature = Recommendations.Signature(missing);
                if (File.Exists(flag) && File.ReadAllText(flag) == signature) return;
                PlayniteApi.Notifications.Add(new NotificationMessage("LayoutStudio.Recommendations", text, NotificationType.Info));
                Directory.CreateDirectory(GetPluginUserDataPath());
                File.WriteAllText(flag, signature);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "LayoutStudio could not announce the recommended add-ons");
            }
        }

        /// <summary>Once, on the first start without any profile: offer to import the settings the user had in ThemeOptions.</summary>
        private void OfferThemeOptionsImport()
        {
            try
            {
                if (studio == null) return;
                string flag = Path.Combine(GetPluginUserDataPath(), "migration-offered.flag");
                if (File.Exists(flag) || studio.Profiles.Names().Count > 0) return;
                string config = Directory.GetDirectories(PlayniteApi.Paths.ExtensionsDataPath, "904cbf3b-*")
                    .Select(d => Path.Combine(d, "config.json")).FirstOrDefault(File.Exists);
                if (config == null) return;
                File.WriteAllText(flag, DateTime.Now.ToString("o"));
                var answer = PlayniteApi.Dialogs.ShowMessage(
                    "Layout Studio found your ThemeOptions settings. Import them as your starting profile?" + Environment.NewLine + Environment.NewLine
                    + "(Your ThemeOptions configuration is only read, never modified.)",
                    "Layout Studio", MessageBoxButton.YesNo);
                if (answer != MessageBoxResult.Yes) return;
                var profile = studio.ImportThemeOptions(File.ReadAllText(config));
                logger.Info("LayoutStudio imported ThemeOptions: " + profile.Overrides.Count + " overrides, " + profile.Presets.Count + " presets");
                if (studio.ProfileApplier.RestartRequired)
                    PlayniteApi.Dialogs.ShowMessage("Imported. Some choices (button style, top panel, neon style) change the shape of controls and apply after you restart Playnite.", "Layout Studio");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "LayoutStudio could not import the ThemeOptions settings");
            }
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            if (PlayniteApi.ApplicationInfo.Mode != ApplicationMode.Desktop)
            {
                // Fullscreen: the editor is a desktop window, so the look is changed from short lists picked with the controller
                if (studio == null) yield break;
                foreach (var entry in QuickMenu.Entries(studio.Schema, studio.Active, key => PlayniteApi.Resources.GetString(key)))
                {
                    var e = entry;
                    yield return new MainMenuItem { Description = e.Title, MenuSection = "@Layout Studio", Action = a => ChooseQuick(e) };
                }
                yield return new MainMenuItem { Description = "Reset to the theme defaults", MenuSection = "@Layout Studio", Action = a => ResetQuick() };
                yield break;
            }
            yield return new MainMenuItem
            {
                Description = "Layout Studio",
                MenuSection = "@Layout Studio",
                Action = a => OpenEditor()
            };
            // Testbed helper: only offered where a sample artwork sits in the configuration folder (never on a real install).
            if (File.Exists(Path.Combine(PlayniteApi.Paths.ConfigurationPath, "sample-bg.jpg")))
                yield return new MainMenuItem { Description = "Add or refill sample game (testbed)", MenuSection = "@Layout Studio", Action = a => AddSampleGame() };
            if (SampleLibrary.Available(PlayniteApi.Paths.ConfigurationPath))
                yield return new MainMenuItem { Description = "Add the sample library (testbed)", MenuSection = "@Layout Studio", Action = a => SampleLibrary.Fill(PlayniteApi, logger) };
            if (File.Exists(Path.Combine(PlayniteApi.Paths.ConfigurationPath, "sample-bg.jpg")))
                yield return new MainMenuItem { Description = "Dump background parts (testbed)", MenuSection = "@Layout Studio", Action = a => DumpBackground() };
        }

        /// <summary>Fullscreen quick menu: shows the choices of one preset group and applies the one picked.</summary>
        private void ChooseQuick(QuickMenuEntry entry)
        {
            try
            {
                var options = entry.Choices.Select(c => new GenericItemOption(c.Name, c.Id == entry.CurrentId ? "Current" : "")).ToList();
                var picked = PlayniteApi.Dialogs.ChooseItemWithSearch(options, query =>
                    options.Where(o => string.IsNullOrEmpty(query) || o.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList(), "", entry.Title);
                if (picked == null) return;
                var choice = entry.Choices.FirstOrDefault(c => c.Name == picked.Name);
                if (choice != null) QuickMenu.Choose(studio.ProfileApplier, studio.Active, entry.Id, choice.Id);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "LayoutStudio quick menu could not apply " + entry.Id);
            }
        }

        private void ResetQuick()
        {
            try
            {
                var answer = PlayniteApi.Dialogs.ShowMessage("Put every Layout Studio setting of this theme back to its default?", "Layout Studio", MessageBoxButton.YesNo);
                if (answer != MessageBoxResult.Yes) return;
                string error = studio.ResetAll();
                if (error != null) PlayniteApi.Dialogs.ShowMessage(error, "Layout Studio");
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "LayoutStudio quick menu could not reset");
            }
        }

        /// <summary>Opens the editor, or brings it to the front; <paramref name="pageId"/> (a page id such as visual-sections) selects the page to show.</summary>
        private void OpenEditor(string pageId = null)
        {
            if (studio == null)
            {
                PlayniteApi.Dialogs.ShowMessage("Layout Studio is idle: it works with the Modula theme, with ThemeOptions disabled. See extensions.log.");
                return;
            }
            if (editor != null) { if (shellModel != null) shellModel.OpenPage(pageId); editor.Activate(); return; }
            logoChecked = false;   // a logo added meanwhile is seen when the editor is opened again
            Action releaseHandlers = null;
            try
            {
                // every view model of the editor listens to the store and the history: detach them all when the window closes
                var releases = new[] { studio.Store.MarkSubscribers(), studio.History.MarkSubscribers(), studio.ProfileApplier.MarkSubscribers() };
                releaseHandlers = () => { foreach (var release in releases) release(); };
                var shell = new ShellViewModel(studio.Schema, studio.Store, studio.History, studio.ProfileApplier, studio.Active,
                    key => Application.Current.TryFindResource(key) as string, CultureInfo.CurrentCulture, SelectedGameBackground,
                    studio, text => { try { Clipboard.SetText(text); } catch (Exception ex) { logger.Warn(ex, "clipboard unavailable"); } },
                    new PlayniteSettingsViewModel(PlayniteSettingsAccess.Create(PlayniteApi), Path.Combine(GetPluginUserDataPath(), "playnite-settings-backup.json")) { ThemeGlassOn = () => studio.Store.Get("WindowGlassEnabled") != "False" },
                    HwinfoCheck.Run(PlayniteApi.Paths.ExtensionsDataPath), exposed.Facts,
                    () => new[]
                    {
                        new KeyValuePair<string, string>("Layout Studio", ExtensionVersion()),
                        new KeyValuePair<string, string>("Playnite", PlayniteApi.ApplicationInfo.ApplicationVersion + " (" + PlayniteApi.ApplicationInfo.Mode + ")"),
                        new KeyValuePair<string, string>("Language", PlayniteApi.ApplicationSettings.Language ?? "?"),
                        new KeyValuePair<string, string>("Log", "extensions.log in Playnite's user data folder")
                    }, CurrentLiveContext);
                shell.OpenPage(pageId);
                shell.Mode = "Desktop";
                if (File.Exists(FullscreenSchemaPath())) shell.ModeSwitch = SwitchEditorMode;
                desktopShell = shell;
                shellModel = shell;
                editor = EditorWindowFactory.Create(shell, PlayniteApi.ApplicationSettings.Language);
                editor.Closed += (s, e) =>
                {
                    releaseHandlers(); editor = null; shellModel = null; desktopShell = null;
                    studio.History.EndAllBatches(); studio.TrySave();
                    if (fsStudio != null) { fsStudio.History.EndAllBatches(); fsStudio.TrySave(); fsStudio = null; studio.ReloadLook(); }
                };
                editor.Show();
            }
            catch (Exception ex)
            {
                editor = null;
                if (releaseHandlers != null) releaseHandlers();
                logger.Error(ex, "LayoutStudio could not open the editor");
                PlayniteApi.Dialogs.ShowErrorMessage("Layout Studio could not open the editor: " + ex.Message);
            }
        }

        // ---- editing the Fullscreen profile from the desktop editor ----------------------------------------------

        private StudioService fsStudio;
        private ShellViewModel desktopShell;

        private string FullscreenThemeDir() { return Path.Combine(PlayniteApi.Paths.ConfigurationPath, "Themes", "Fullscreen", ThemeGuard.FullscreenThemeId); }
        private string FullscreenSchemaPath() { return Path.Combine(FullscreenThemeDir(), "layout-studio.schema.json"); }

        /// <summary>Switches the open editor between the Desktop theme (live) and the Fullscreen theme (its profile is edited and saved; it shows when Playnite runs in Fullscreen mode).</summary>
        private void SwitchEditorMode(string mode)
        {
            if (editor == null || desktopShell == null) return;
            try
            {
                if (mode == "Fullscreen")
                {
                    studio.TrySave();   // the shared Look is read from the file: write what is pending first
                    fsStudio = StudioService.CreateOffline(FullscreenThemeDir(), Path.Combine(GetPluginUserDataPath(), "fullscreen"), Application.Current.Dispatcher, Path.Combine(GetPluginUserDataPath(), "look.profile.json"));
                    fsStudio.Log = line => logger.Warn(line);
                    fsStudio.ProfileApplier.Log = line => logger.Warn(line);
                    fsStudio.LoadActive();
                    var fsShell = new ShellViewModel(fsStudio.Schema, fsStudio.Store, fsStudio.History, fsStudio.ProfileApplier, fsStudio.Active,
                        key => Application.Current.TryFindResource(key) as string, CultureInfo.CurrentCulture, null,
                        fsStudio, text => { try { Clipboard.SetText(text); } catch (Exception ex) { logger.Warn(ex, "clipboard unavailable"); } },
                        null, null, null,
                        () => new[]
                        {
                            new KeyValuePair<string, string>("Editing", "the Fullscreen profile (shown when Playnite runs in Fullscreen mode)"),
                            new KeyValuePair<string, string>("Playnite", PlayniteApi.ApplicationInfo.ApplicationVersion + " (" + PlayniteApi.ApplicationInfo.Mode + ")")
                        }, null);
                    fsShell.Mode = "Fullscreen";
                    fsShell.ModeSwitch = SwitchEditorMode;
                    shellModel = fsShell;
                    editor.DataContext = fsShell;
                    editor.Title = "Layout Studio - Fullscreen";
                }
                else
                {
                    if (fsStudio != null) { fsStudio.History.EndAllBatches(); fsStudio.TrySave(); fsStudio = null; studio.ReloadLook(); }
                    shellModel = desktopShell;
                    editor.DataContext = desktopShell;
                    editor.Title = "Layout Studio";
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "LayoutStudio could not switch the editor to " + mode);
                PlayniteApi.Dialogs.ShowErrorMessage("Layout Studio could not switch to " + mode + ": " + ex.Message);
            }
        }

        /// <summary>The version in extension.yaml next to the plugin dll ("?" when it cannot be read).</summary>
        private static string ExtensionVersion()
        {
            try
            {
                string yaml = Path.Combine(Path.GetDirectoryName(typeof(LayoutStudioPlugin).Assembly.Location), "extension.yaml");
                foreach (var line in File.ReadAllLines(yaml))
                    if (line.StartsWith("Version:", StringComparison.OrdinalIgnoreCase)) return line.Substring(8).Trim().Trim('\'', '"');
            }
            catch (Exception) { }
            return "?";
        }

        /// <summary>What the main window shows now (view, and what the selected game has), for the Sizes page: which sizes can be seen live.</summary>
        private LiveContext CurrentLiveContext()
        {
            var main = PlayniteApi.MainView;
            var context = new LiveContext { View = main.ActiveDesktopView.ToString().ToLowerInvariant() };
            var game = main.SelectedGames == null ? null : main.SelectedGames.FirstOrDefault();
            if (game == null) return context;
            context.HasGame = true;
            context.HasDescription = !string.IsNullOrWhiteSpace(game.Description);
            context.HasLinks = game.Links != null && game.Links.Count > 0;
            // the disk is asked once per selected game, not every few seconds
            if (!logoChecked || logoGame != game.Id)
            {
                logoGame = game.Id;
                logoChecked = true;
                try
                {
                    // Extra Metadata Loader keeps logos in <user data>\ExtraMetadata\games\<game id>\Logo.*
                    string folder = Path.Combine(PlayniteApi.Paths.ConfigurationPath, "ExtraMetadata", "games", game.Id.ToString());
                    logoFound = Directory.Exists(folder) && Directory.GetFiles(folder, "Logo.*").Length > 0;
                }
                catch (Exception) { logoFound = false; }
            }
            context.HasLogo = logoFound;
            return context;
        }

        private Guid logoGame;
        private bool logoChecked, logoFound;

        /// <summary>Artwork of the selected game for the background preview; null when there is none.</summary>
        private string SelectedGameBackground()
        {
            var game = PlayniteApi.MainView.SelectedGames == null ? null : PlayniteApi.MainView.SelectedGames.FirstOrDefault();
            if (game == null || string.IsNullOrEmpty(game.BackgroundImage)) return null;
            return PlayniteApi.Database.GetFullFilePath(game.BackgroundImage);
        }

        /// <summary>Testbed probe: logs what Playnite does to the elements named PART_ImageBackground (blur, darkening).</summary>
        private void DumpBackground()
        {
            var api = PlayniteApi.ApplicationSettings;
            logger.Info("probe: ApplicationSettings is " + api.GetType().FullName);
            foreach (var f in api.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                logger.Info("probe:   field " + f.Name + " : " + f.FieldType.FullName);
            foreach (var pr in api.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                if (pr.Name.IndexOf("Blur", StringComparison.OrdinalIgnoreCase) >= 0 || pr.Name.IndexOf("Dark", StringComparison.OrdinalIgnoreCase) >= 0)
                    logger.Info("probe:   property " + pr.Name + " canWrite=" + pr.CanWrite);
            Action<System.Windows.DependencyObject, int> walk = null;
            walk = (node, depth) =>
            {
                var fe = node as FrameworkElement;
                if (fe != null && (fe.Name == "PART_ImageBackground" || fe.GetType().Name == "FadeImage"))
                {
                    for (System.Windows.DependencyObject up = fe; up != null; up = System.Windows.Media.VisualTreeHelper.GetParent(up))
                    {
                        var u = up as UIElement;
                        if (u == null) continue;
                        var blur = u.Effect as System.Windows.Media.Effects.BlurEffect;
                        logger.Info("probe: [" + fe.Name + "] " + up.GetType().Name + " name=" + ((up as FrameworkElement) == null ? "" : ((FrameworkElement)up).Name)
                            + " opacity=" + u.Opacity + " effect=" + (u.Effect == null ? "none" : u.Effect.GetType().Name + (blur != null ? " r=" + blur.Radius : "")));
                        var panel = System.Windows.Media.VisualTreeHelper.GetParent(up) as System.Windows.Controls.Panel;
                        if (panel != null)
                            foreach (UIElement sib in panel.Children)
                                if (!ReferenceEquals(sib, up)) logger.Info("probe:   sibling " + sib.GetType().Name + " opacity=" + sib.Opacity + " visible=" + sib.IsVisible);
                    }
                    return;
                }
                int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < n; i++) walk(System.Windows.Media.VisualTreeHelper.GetChild(node, i), depth + 1);
            };
            foreach (Window w in Application.Current.Windows) { logger.Info("probe: window " + w.GetType().Name + " visible=" + w.IsVisible); walk(w, 0); }
            logger.Info("probe: done");
        }

        /// <summary>Testbed helper: creates (or refills) "Sample game" with a few entries in every metadata category, so each part of the theme has something to show.</summary>
        private void AddSampleGame()
        {
            string image = Path.Combine(PlayniteApi.Paths.ConfigurationPath, "sample-bg.jpg");
            if (!File.Exists(image)) { logger.Warn("sample game: " + image + " not found"); return; }
            var db = PlayniteApi.Database;
            var game = db.Games.FirstOrDefault(g => g.Name == "Sample game");
            bool created = game == null;
            if (created)
            {
                game = new Game("Sample game") { IsInstalled = true };
                db.Games.Add(game);
                game.BackgroundImage = db.AddFile(image, game.Id);
                game.CoverImage = db.AddFile(image, game.Id);
                game.Icon = db.AddFile(image, game.Id);
            }
            game.GenreIds = Ids(db.Genres, "Action", "Adventure", "Role-playing");
            game.CategoryIds = Ids(db.Categories, "Favorites", "To finish", "Co-op nights");
            game.TagIds = Ids(db.Tags, "Atmospheric", "Great soundtrack", "Open world");
            game.FeatureIds = Ids(db.Features, "Single Player", "Multiplayer", "Full Controller Support");
            game.DeveloperIds = Ids(db.Companies, "Lantern Works", "Moss Studio");
            game.PublisherIds = Ids(db.Companies, "Paper Boat Games", "Northern Press");
            game.PlatformIds = Ids(db.Platforms, "PC (Windows)", "Nintendo Switch");
            game.SeriesIds = Ids(db.Series, "Sample Saga", "Sample Tales");
            game.AgeRatingIds = Ids(db.AgeRatings, "PEGI 12", "ESRB T");
            game.RegionIds = Ids(db.Regions, "Europe", "North America");
            game.SourceId = Ids(db.Sources, "Sample Library")[0];
            game.CompletionStatusId = Ids(db.CompletionStatuses, "Playing")[0];
            game.ReleaseDate = new ReleaseDate(2021, 6, 15);
            game.Playtime = 5 * 3600 + 25 * 60;
            game.PlayCount = 14;
            game.LastActivity = DateTime.Now.AddDays(-2);
            game.Added = DateTime.Now.AddDays(-40);
            game.CommunityScore = 86;
            game.CriticScore = 79;
            game.UserScore = 90;
            game.Version = "1.4.2";
            game.InstallSize = 7UL * 1024 * 1024 * 1024;
            game.Description = "<p>A short sample description so the description area has text to show.</p><p>Second paragraph with <b>bold</b> and <i>italic</i> words.</p>";
            game.Notes = "Sample note: finish the lighthouse chapter.";
            game.Links = new System.Collections.ObjectModel.ObservableCollection<Link>
            {
                new Link("Website", "https://example.com"), new Link("Wiki", "https://example.com/wiki"), new Link("Forum", "https://example.com/forum")
            };
            db.Games.Update(game);
            logger.Info("sample game " + (created ? "added" : "refilled"));
        }

        private static System.Collections.Generic.List<Guid> Ids<T>(IItemCollection<T> collection, params string[] names) where T : DatabaseObject
        {
            var ids = new System.Collections.Generic.List<Guid>();
            foreach (var name in names)
            {
                var item = collection.FirstOrDefault(i => i.Name == name) ?? collection.Add(name);
                ids.Add(item.Id);
            }
            return ids;
        }
    }
}
