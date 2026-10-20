using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;
using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>Data-object formats and the contract between a drag source view model and the drag behavior.</summary>
    public static class DragFormats
    {
        public const string Section = "LayoutStudio.Section";
        public const string Tile = "LayoutStudio.Tile";
        public const string Slot = "LayoutStudio.Slot";
    }

    public interface IDragSource
    {
        string DragFormat { get; }
        string DragPayload { get; }
    }

    /// <summary>A view model whose edits made during a pointer gesture (a resize drag) form one undo step.</summary>
    public interface IGestureTarget
    {
        void BeginGesture();
        void EndGesture();
    }

    /// <summary>The window-background sliders plus a small preview that reacts as they move.</summary>
    public class BackgroundEditorViewModel : ViewModelBase
    {
        private const double PreviewScale = 0.2;   // the preview is about a fifth of the window
        private readonly ConstantStore store;
        private readonly Func<string> imagePath;
        private readonly History history;

        public BackgroundEditorViewModel(ConstantStore store, Schema schema, History history, ProfileApplier applier, Profile profile,
            Func<string, string> localize, CultureInfo culture, Func<string> imagePath)
        {
            this.store = store;
            this.imagePath = imagePath;
            this.history = history;
            SetWindowCommand = new RelayCommand(() => SetMode("window"));
            SetPageCommand = new RelayCommand(() => SetMode("page"));
            SetOffCommand = new RelayCommand(() => SetMode("off"));
            Items = schema.Tuning.Select(t => new SettingViewModel(
                new Control { Kind = t.Kind, Id = t.Key, Keys = new List<string> { t.Key }, Title = t.Title }, store, history, applier, profile, localize, culture)).ToList();
            var keys = new HashSet<string>(schema.Tuning.Select(t => t.Key).Concat(new[] { "PageGlassOpacity" }));
            store.ValueChanged += key => { if (keys.Contains(key)) { RaiseAll(); QueueLiveRefresh(); } };
            RefreshLive();
            QueueLiveRefresh();   // once more shortly after: the window may still be loading when the editor opens right at start-up
        }

        /// <summary>
        /// Takes a picture of the real window (set by the plugin): the preview shows the game's own artwork through the real layers and panels,
        /// not a drawing of them. Null when there is no window to draw (then the stand-in drawing stays).
        /// </summary>
        public static Func<ImageSource> LiveCapture { get; set; }

        /// <summary>Runs an action a moment later, on the UI thread (the theme applies a changed setting asynchronously). Replaceable for tests.</summary>
        public static Action<Action> LiveSchedule { get; set; }

        private ImageSource live;
        private bool liveQueued;

        public ImageSource LivePreview { get { return live; } }
        public bool HasLivePreview { get { return live != null; } }
        public bool NoLivePreview { get { return live == null; } }

        /// <summary>Captures the real window now.</summary>
        public void RefreshLive()
        {
            ImageSource shot = null;
            try { if (LiveCapture != null) shot = LiveCapture(); } catch (Exception) { shot = null; }
            live = shot;
            Raise("LivePreview"); Raise("HasLivePreview"); Raise("NoLivePreview");
        }

        private void QueueLiveRefresh()
        {
            if (LiveCapture == null || liveQueued) return;
            liveQueued = true;
            Action run = () => { liveQueued = false; RefreshLive(); };
            if (LiveSchedule != null) { LiveSchedule(run); return; }
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            timer.Tick += (o, e) => { timer.Stop(); run(); };
            timer.Start();
        }

        public IList<SettingViewModel> Items { get; private set; }

        /// <summary>
        /// The sliders of the page for the chosen mode: the whole-window artwork and the game-page artwork each have their own blur and
        /// darkness, so the sliders must act on the layer that is showing (otherwise they read 0 while the page stays blurred and dark).
        /// </summary>
        public IList<SettingViewModel> Sliders
        {
            get
            {
                var keys = SliderKeys(Mode);
                return keys.Select(k => Items.FirstOrDefault(i => i.Id == k)).Where(i => i != null).ToList();
            }
        }

        private static string[] SliderKeys(string mode)
        {
            // darkness by zone: each panel's own background opacity (the zones are the same in every mode)
            var zones = new[] { "TopPanelGlassOpacity", "SidebarGlassOpacity", "LibraryPanelGlassOpacity", "FilterPanelGlassOpacity", "DetailsPageScrimOpacity" };
            if (mode == "window") return new[] { "WindowGlassBlurRadius", "WindowGlassScrimOpacity" }.Concat(zones).ToArray();
            if (mode == "page") return new[] { "PageGlassBlurRadius", "PageGlassScrimOpacity" }.Concat(zones).ToArray();
            return zones;
        }

        /// <summary>Where the artwork is drawn: "window" (behind everything), "page" (only behind the game page) or "off" (nowhere).</summary>
        public string Mode
        {
            get
            {
                if (store.Get("WindowGlassEnabled") == "True") return "window";
                return double.Parse(store.Get("PageGlassOpacity"), CultureInfo.InvariantCulture) > 0 ? "page" : "off";
            }
        }

        public bool IsModeWindow { get { return Mode == "window"; } }
        public bool IsModePage { get { return Mode == "page"; } }
        public bool IsModeOff { get { return Mode == "off"; } }
        public RelayCommand SetWindowCommand { get; private set; }
        public RelayCommand SetPageCommand { get; private set; }
        public RelayCommand SetOffCommand { get; private set; }

        private void SetMode(string mode)
        {
            string error;
            history.BeginBatch();
            try
            {
                store.TrySet("WindowGlassEnabled", mode == "window" ? "True" : "False", out error);
                if (mode == "off") store.TrySet("PageGlassOpacity", "0", out error);
                else if (mode == "page" && double.Parse(store.Get("PageGlassOpacity"), CultureInfo.InvariantCulture) <= 0) store.Reset("PageGlassOpacity");
                else if (mode == "window" && double.Parse(store.Get("PageGlassOpacity"), CultureInfo.InvariantCulture) <= 0) store.Reset("PageGlassOpacity");
            }
            finally { history.EndBatch(); }
            RaiseAll();
            QueueLiveRefresh();
        }

        public bool PreviewEnabled { get { return Mode != "off"; } }
        public double PreviewBlurRadius { get { return Number(Mode == "page" ? "PageGlassBlurRadius" : "WindowGlassBlurRadius") * PreviewScale; } }
        public double PreviewScrimOpacity { get { return Number(Mode == "page" ? "PageGlassScrimOpacity" : "WindowGlassScrimOpacity"); } }
        public double PreviewTopOpacity { get { return Number("TopPanelGlassOpacity"); } }
        public double PreviewSidebarOpacity { get { return Number("SidebarGlassOpacity"); } }

        /// <summary>Path of the selected game's artwork for the preview, when there is one on disk.</summary>
        public string PreviewImagePath
        {
            get
            {
                string path = imagePath == null ? null : imagePath();
                return path != null && File.Exists(path) ? path : null;
            }
        }

        public bool HasPreviewImage { get { return PreviewImagePath != null; } }

        private double Number(string key)
        {
            return double.Parse(store.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
