using System;
using System.Collections.Generic;

namespace LayoutStudio.Core
{
    /// <summary>A rectangle of pixels.</summary>
    public struct PixelBox
    {
        public int X, Y, Width, Height;
        public bool IsEmpty { get { return Width <= 0 || Height <= 0; } }
        public override string ToString() { return X + "," + Y + " " + Width + "x" + Height; }
    }

    /// <summary>
    /// Where the pictures of a preset group differ. The previews of one group are the same screenshot with one setting changed,
    /// so showing the whole picture hides the change: the tiles show only the part of it that moves.
    /// </summary>
    public static class PreviewDiff
    {
        /// <summary>
        /// The smallest box that holds every pixel that differs between any two of the frames (BGRA, four bytes a pixel, same size).
        /// The box is padded, kept at least <paramref name="minWidth"/> x <paramref name="minHeight"/> around its centre when the picture allows it,
        /// and clamped to the picture. Empty when the frames are the same (or fewer than two).
        /// </summary>
        public static PixelBox Box(IList<byte[]> frames, int width, int height, int threshold = 12, int pad = 8, int minWidth = 120, int minHeight = 56)
        {
            var none = new PixelBox();
            if (frames == null || frames.Count < 2 || width <= 0 || height <= 0) return none;
            foreach (var f in frames) if (f == null || f.Length < width * height * 4) return none;
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    bool differs = false;
                    for (int k = 1; k < frames.Count && !differs; k++)
                    {
                        int d = Math.Abs(frames[0][i] - frames[k][i]) + Math.Abs(frames[0][i + 1] - frames[k][i + 1]) + Math.Abs(frames[0][i + 2] - frames[k][i + 2]);
                        if (d > threshold) differs = true;
                    }
                    if (!differs) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) return none;
            int left = Math.Max(0, minX - pad), top = Math.Max(0, minY - pad);
            int right = Math.Min(width, maxX + 1 + pad), bottom = Math.Min(height, maxY + 1 + pad);
            int w = right - left, h = bottom - top;
            if (w < minWidth) { int grow = Math.Min(width, minWidth) - w; left = Math.Max(0, Math.Min(width - Math.Min(width, minWidth), left - grow / 2)); w = Math.Min(width, minWidth); }
            if (h < minHeight) { int grow = Math.Min(height, minHeight) - h; top = Math.Max(0, Math.Min(height - Math.Min(height, minHeight), top - grow / 2)); h = Math.Min(height, minHeight); }
            return new PixelBox { X = left, Y = top, Width = w, Height = h };
        }
    }
}
