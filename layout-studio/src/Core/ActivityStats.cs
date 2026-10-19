using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace LayoutStudio.Core
{
    public enum ColorLevel { None, Red, Orange, Green, Blue }

    /// <summary>Count, median, minimum and maximum of one logged metric.</summary>
    public class MetricSummary
    {
        public int Count;
        public double Median;
        public double Min;
        public double Max;
        public bool HasData { get { return Count > 0; } }

        public static MetricSummary From(IList<double> values)
        {
            var s = new MetricSummary();
            if (values.Count == 0) return s;
            var sorted = values.OrderBy(v => v).ToList();
            s.Count = sorted.Count;
            s.Min = sorted[0];
            s.Max = sorted[sorted.Count - 1];
            int mid = sorted.Count / 2;
            s.Median = sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
            return s;
        }
    }

    /// <summary>What GameActivity logged for one game, pooled over all its sessions.</summary>
    public class GameFacts
    {
        public MetricSummary Fps = new MetricSummary();
        public MetricSummary Cpu = new MetricSummary();
        public MetricSummary Gpu = new MetricSummary();
        public MetricSummary Ram = new MetricSummary();
        public bool HasAnyData { get { return Fps.HasData || Cpu.HasData || Gpu.HasData || Ram.HasData; } }
    }

    /// <summary>
    /// Reads a GameActivity per-game file (Items[].Details[] samples with fps, cpu, gpu, ram) and summarizes it. The logger writes 0 when a
    /// sensor was not available, so zeros are left out: fps, gpu on their own, cpu and ram only when the row has any load at all.
    /// </summary>
    public static class ActivityStats
    {
        /// <param name="since">Only samples logged at or after this instant count (older ones came from a sensor mapping that was wrong); null = all.</param>
        public static GameFacts FromJson(string json, DateTime? since = null)
        {
            var facts = new GameFacts();
            if (string.IsNullOrEmpty(json)) return facts;
            object root;
            try { root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 }.DeserializeObject(json); }
            catch (Exception) { return facts; }
            var top = root as IDictionary<string, object>;
            if (top == null) return facts;
            object itemsValue;
            if (!top.TryGetValue("Items", out itemsValue)) return facts;
            var items = itemsValue as System.Collections.IEnumerable;
            if (items == null) return facts;

            var fps = new List<double>(); var cpu = new List<double>(); var gpu = new List<double>(); var ram = new List<double>();
            foreach (var item in items)
            {
                var session = item as IDictionary<string, object>;
                object detailsValue;
                if (session == null || !session.TryGetValue("Details", out detailsValue)) continue;
                var details = detailsValue as System.Collections.IEnumerable;
                if (details == null) continue;
                foreach (var d in details)
                {
                    var sample = d as IDictionary<string, object>;
                    if (sample == null) continue;
                    if (since.HasValue && !LoggedSince(sample, since.Value)) continue;
                    double f = Num(sample, "fps"), c = Num(sample, "cpu"), g = Num(sample, "gpu"), r = Num(sample, "ram");
                    if (f > 0) fps.Add(f);
                    if (g > 0) gpu.Add(g);
                    if (c > 0) cpu.Add(c);   // each sensor on its own: one that is not measured is a missing value, not a 0 % load
                    if (r > 0) ram.Add(r);
                }
            }
            facts.Fps = MetricSummary.From(fps);
            facts.Cpu = MetricSummary.From(cpu);
            facts.Gpu = MetricSummary.From(gpu);
            facts.Ram = MetricSummary.From(ram);
            return facts;
        }

        private static bool LoggedSince(IDictionary<string, object> sample, DateTime since)
        {
            object v;
            DateTime when;
            if (!sample.TryGetValue("datelog", out v) || !(v is string)) return false;
            return DateTime.TryParse((string)v, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out when) && when >= since.ToUniversalTime();
        }

        private static double Num(IDictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v) || v == null || v is string) return 0;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return 0; }
        }
    }

    /// <summary>Turns a number into a colour level from three limits. Limits typed in the wrong order are put in order.</summary>
    public class ColorScale
    {
        private readonly double a, b, c;
        private readonly bool higherIsBetter;

        private ColorScale(bool higherIsBetter, double x, double y, double z)
        {
            this.higherIsBetter = higherIsBetter;
            var sorted = new[] { x, y, z }.OrderBy(v => v).ToArray();
            a = sorted[0]; b = sorted[1]; c = sorted[2];
        }

        /// <summary>Frame rate: red below <paramref name="redBelow"/>, orange up to <paramref name="orangeUpTo"/>, green above, blue from <paramref name="blueFrom"/>.</summary>
        public static ColorScale Fps(double redBelow, double orangeUpTo, double blueFrom) { return new ColorScale(true, redBelow, orangeUpTo, blueFrom); }

        /// <summary>Load in percent: green up to <paramref name="warn"/>, orange up to <paramref name="crit"/>, red above.</summary>
        public static ColorScale Load(double warn, double crit) { return new ColorScale(false, warn, crit, double.MaxValue); }

        public ColorLevel Level(double? value)
        {
            if (!value.HasValue) return ColorLevel.None;
            double v = value.Value;
            if (higherIsBetter)
            {
                if (v < a) return ColorLevel.Red;
                if (v <= b) return ColorLevel.Orange;
                if (v >= c) return ColorLevel.Blue;
                return ColorLevel.Green;
            }
            if (v <= a) return ColorLevel.Green;
            if (v <= b) return ColorLevel.Orange;
            return ColorLevel.Red;
        }
    }
}
