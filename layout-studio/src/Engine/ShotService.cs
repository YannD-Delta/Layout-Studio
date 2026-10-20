using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// Test aid, off unless the environment variable LAYOUTSTUDIO_SHOT_DIR names a folder: when a file "shot.request" appears there, the main
    /// window is rendered by WPF itself to the PNG path written in that file, then the request is deleted. It works while the monitor is asleep
    /// (a screen capture then returns an old frame), and it never records any other window.
    /// </summary>
    public static class ShotService
    {
        public static void StartIfRequested(Dispatcher ui, Action<string> log)
        {
            string dir = Environment.GetEnvironmentVariable("LAYOUTSTUDIO_SHOT_DIR");
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            string request = Path.Combine(dir, "shot.request");
            var timer = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (s, e) =>
            {
                string target = null;
                try
                {
                    if (!File.Exists(request)) return;
                    target = File.ReadAllText(request).Trim();
                    File.Delete(request);
                    target = Save(target);
                    File.WriteAllText(target + ".ok", "ok");
                }
                catch (Exception ex)
                {
                    if (log != null) log("shot failed: " + ex.Message);
                    try { if (target != null) File.WriteAllText(target + ".err", ex.ToString()); } catch (Exception) { }
                }
            };
            timer.Start();
            string keys = Path.Combine(dir, "keys.request");
            var keyTimer = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromMilliseconds(400) };
            bool busy = false;
            keyTimer.Tick += async (s, e) =>
            {
                if (busy || !File.Exists(keys)) return;
                busy = true;
                try
                {
                    string text = File.ReadAllText(keys);
                    File.Delete(keys);
                    foreach (var token in text.Split(new[] { ' ', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (token.StartsWith("sleep:")) { await Task.Delay(int.Parse(token.Substring(6))); continue; }
                        var key = (Key)Enum.Parse(typeof(Key), token, true);
                        ui.BeginInvoke(new Action(() => Press(key)));   // a key may open a modal dialog: never block this handler on it
                        await Task.Delay(250);
                    }
                    File.WriteAllText(keys + ".done", "ok");
                }
                catch (Exception ex) { if (log != null) log("keys failed: " + ex.Message); }
                busy = false;
            };
            keyTimer.Start();
            if (log != null) log("LayoutStudio shots enabled in " + dir);
        }

        /// <summary>Raises a key down and up on the focused element of the active window (no real keyboard, so no other program gets the key).</summary>
        private static void Press(Key key)
        {
            var window = Application.Current.Windows.OfType<Window>().LastOrDefault(w => w.IsActive && w.IsVisible) ?? Application.Current.MainWindow;
            var target = Keyboard.FocusedElement ?? window;
            var source = PresentationSource.FromVisual((Visual)(target is Visual ? target : window));
            foreach (var routed in new[] { Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent, Keyboard.PreviewKeyUpEvent, Keyboard.KeyUpEvent })
                target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = routed });
        }

        private static string Save(string path)
        {
            // "Layout Studio|C:\x.png" draws only the window with that title (the editor), at its own size
            int bar = path.IndexOf('|');
            if (bar > 0)
            {
                string title = path.Substring(0, bar);
                path = path.Substring(bar + 1);
                var only = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsVisible && x.Title != null && x.Title.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0);
                if (only == null) throw new InvalidOperationException("no visible window titled " + title);
                var shot = new RenderTargetBitmap((int)only.ActualWidth, (int)only.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                shot.Render(only);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(shot));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = File.Create(path)) enc.Save(stream);
                return path;
            }
            var main = Application.Current.MainWindow;
            if (main == null) throw new InvalidOperationException("no main window");
            int w = (int)main.ActualWidth, h = (int)main.ActualHeight;
            // Playnite shows the game details, menus and dialogs as windows of their own: draw every visible window of the process,
            // the main one first, at its place on the screen.
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var list = new System.Collections.Generic.List<Window> { main };
                foreach (Window win in Application.Current.Windows) if (win != main && win.IsVisible && win.ActualWidth > 0) list.Add(win);
                foreach (var win in list)
                {
                    var one = new RenderTargetBitmap((int)win.ActualWidth, (int)win.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    one.Render(win);
                    double x = win == main ? 0 : win.Left - main.Left, y = win == main ? 0 : win.Top - main.Top;
                    dc.DrawImage(one, new Rect(x, y, win.ActualWidth, win.ActualHeight));
                }
            }
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = File.Create(path)) encoder.Save(stream);
            return path;
        }
    }
}
