using System.Collections.Generic;
using Playnite.SDK;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// What the Fullscreen details page binds for its Media row: {PluginSettings Plugin=LayoutStudio, Path=Media.Screenshots}.
    /// Updated by the plugin when the selected game changes.
    /// </summary>
    public class MediaViewModel : ObservableObject
    {
        private List<string> screenshots = new List<string>();
        private string video;

        public List<string> Screenshots { get { return screenshots; } private set { screenshots = value; OnPropertyChanged(); OnPropertyChanged("HasScreenshots"); } }
        public bool HasScreenshots { get { return screenshots != null && screenshots.Count > 0; } }
        public string Video { get { return video; } private set { video = value; OnPropertyChanged(); OnPropertyChanged("HasVideo"); } }
        public bool HasVideo { get { return !string.IsNullOrEmpty(video); } }

        public void Update(List<string> newScreenshots, string newVideo)
        {
            Screenshots = newScreenshots ?? new List<string>();
            Video = newVideo;
        }
    }
}
