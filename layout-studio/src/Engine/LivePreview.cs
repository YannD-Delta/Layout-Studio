using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// A picture of the real Playnite window for the editor's Background page: the game's own artwork through the real glass layers, scrims and
    /// panels (what a setting really does), instead of a drawing that approximates them. The window is drawn at a reduced size through a
    /// VisualBrush, so effects scale with it.
    /// </summary>
    public static class LivePreview
    {
        /// <summary>The main window as a bitmap at most <paramref name="maxWidth"/> pixels wide; null when it cannot be drawn now.</summary>
        public static ImageSource Capture(double maxWidth)
        {
            try
            {
                var main = Application.Current == null ? null : Application.Current.MainWindow;
                if (main == null || !main.IsVisible || main.ActualWidth < 50 || main.ActualHeight < 50 || main.WindowState == WindowState.Minimized) return null;
                // the window is drawn at its own size (a VisualBrush of a window leaves its content out), then reduced for the editor
                var full = new RenderTargetBitmap((int)Math.Round(main.ActualWidth), (int)Math.Round(main.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                full.Render(main);
                double scale = Math.Min(1.0, maxWidth / main.ActualWidth);
                if (scale >= 1.0) { full.Freeze(); return full; }
                int w = Math.Max(1, (int)Math.Round(full.PixelWidth * scale)), h = Math.Max(1, (int)Math.Round(full.PixelHeight * scale));
                var visual = new DrawingVisual();
                RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
                using (var dc = visual.RenderOpen()) dc.DrawImage(full, new Rect(0, 0, w, h));
                var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(visual);
                bmp.Freeze();
                return bmp;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
