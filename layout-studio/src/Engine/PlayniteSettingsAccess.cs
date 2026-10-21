using System;
using System.Collections.Generic;
using System.Reflection;
using LayoutStudio.Core;
using LayoutStudio.Editor;
using Playnite.SDK;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// Reads and writes Playnite's live settings object. The SDK only exposes read-only getters, so this reaches the PlayniteSettings
    /// instance behind IPlayniteSettingsAPI by reflection. Setting a property raises Playnite's own change notification, and Playnite
    /// saves its settings when it closes. Everything is guarded: if Playnite changes its internals, Create returns null.
    /// </summary>
    public class PlayniteSettingsAccess : IPlayniteSettingsAccess
    {
        private readonly object settings;

        private PlayniteSettingsAccess(object settings) { this.settings = settings; }

        /// <summary>The live PlayniteSettings object itself (for the testbed scene driver), or null when it cannot be reached.</summary>
        public static object Raw(IPlayniteAPI api)
        {
            try
            {
                var field = api.ApplicationSettings.GetType().GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                return field == null ? null : field.GetValue(api.ApplicationSettings);
            }
            catch (Exception) { return null; }
        }

        public static PlayniteSettingsAccess Create(IPlayniteAPI api)
        {
            try
            {
                var field = api.ApplicationSettings.GetType().GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                object target = field == null ? null : field.GetValue(api.ApplicationSettings);
                if (target == null) return null;
                foreach (var name in AppearanceCheck.SettingNames)
                    if (target.GetType().GetProperty(name) == null) return null;
                return new PlayniteSettingsAccess(target);
            }
            catch (Exception) { return null; }
        }

        public IDictionary<string, object> Read()
        {
            var values = new Dictionary<string, object>();
            foreach (var name in AppearanceCheck.SettingNames)
                values[name] = settings.GetType().GetProperty(name).GetValue(settings, null);
            return values;
        }

        public bool TrySet(string name, object value, out string error)
        {
            error = null;
            var property = settings.GetType().GetProperty(name);
            if (property == null || !property.CanWrite) { error = "not writable"; return false; }
            try { property.SetValue(settings, Convert.ChangeType(value, property.PropertyType), null); return true; }
            catch (Exception ex) { error = ex.InnerException != null ? ex.InnerException.Message : ex.Message; return false; }
        }
    }
}
