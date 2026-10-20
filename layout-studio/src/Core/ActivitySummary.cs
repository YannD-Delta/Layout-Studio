using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace LayoutStudio.Core
{
    /// <summary>What the info cards "Last session length" and "Recent activity" show, read from the GameActivity data of one game.</summary>
    public class ActivitySummary
    {
        public int Sessions;
        public DateTime? LastSession;
        public double LastSessionSeconds;
        /// <summary>Sessions that started within the last <see cref="RecentDays"/> days, and their total length.</summary>
        public int RecentSessions;
        public double RecentSeconds;
        public const int RecentDays = 30;

        public bool HasData { get { return Sessions > 0; } }

        public static ActivitySummary FromJson(string json, DateTime now)
        {
            var summary = new ActivitySummary();
            if (string.IsNullOrEmpty(json)) return summary;
            object root;
            try { root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 }.DeserializeObject(json); }
            catch (Exception) { return summary; }
            var top = root as IDictionary<string, object>;
            object itemsValue;
            if (top == null || !top.TryGetValue("Items", out itemsValue)) return summary;
            var items = itemsValue as System.Collections.IEnumerable;
            if (items == null) return summary;
            foreach (var item in items)
            {
                var session = item as IDictionary<string, object>;
                if (session == null) continue;
                object when, seconds;
                DateTime start;
                if (!session.TryGetValue("DateSession", out when) || !(when is string)
                    || !DateTime.TryParse((string)when, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out start)) continue;
                double length = 0;
                if (session.TryGetValue("ElapsedSeconds", out seconds) && seconds != null && !(seconds is string))
                {
                    try { length = Convert.ToDouble(seconds, CultureInfo.InvariantCulture); } catch (Exception) { length = 0; }
                }
                summary.Sessions++;
                if (!summary.LastSession.HasValue || start > summary.LastSession.Value)
                {
                    summary.LastSession = start;
                    summary.LastSessionSeconds = length;
                }
                if (start >= now.ToUniversalTime().AddDays(-RecentDays) && start <= now.ToUniversalTime().AddDays(1))
                {
                    summary.RecentSessions++;
                    summary.RecentSeconds += length;
                }
            }
            return summary;
        }

        /// <summary>"1h 12m", "45m", "30s": the way the theme writes a play time.</summary>
        public static string Duration(double seconds)
        {
            if (seconds <= 0) return null;
            var t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1) return ((int)t.TotalHours).ToString(CultureInfo.InvariantCulture) + "h " + t.Minutes.ToString(CultureInfo.InvariantCulture) + "m";
            if (t.TotalMinutes >= 1) return t.Minutes.ToString(CultureInfo.InvariantCulture) + "m";
            return t.Seconds.ToString(CultureInfo.InvariantCulture) + "s";
        }
    }
}
