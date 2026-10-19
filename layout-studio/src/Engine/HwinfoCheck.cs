using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using LayoutStudio.Core;
using Microsoft.Win32;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// Reads GameActivity's settings and HWiNFO's gadget list (read only) and reports metrics whose gadget position holds another kind of
    /// sensor. Only the gadget mode is checked; other monitoring sources are left alone.
    /// </summary>
    public static class HwinfoCheck
    {
        private const string GameActivityId = "afbb1a0d-04a1-4d0c-9afa-c6e42ca855b4";
        private static readonly string[] Metrics = { "fps", "cpu", "cpuT", "cpuP", "gpu", "gpuT", "gpuP", "ram" };

        public static IList<string> Run(string extensionsDataPath)
        {
            try
            {
                string config = Path.Combine(extensionsDataPath, GameActivityId, "config.json");
                if (!File.Exists(config)) return new List<string>();
                string text;
                using (var stream = new FileStream(config, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream)) text = reader.ReadToEnd();
                var d = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(text) as IDictionary<string, object>;
                object gadgetOn;
                if (d == null || !d.TryGetValue("UseHWiNFOGadget", out gadgetOn) || !(gadgetOn is bool) || !(bool)gadgetOn) return new List<string>();
                var indexes = new Dictionary<string, int>();
                foreach (var metric in Metrics)
                {
                    object v;
                    if (d.TryGetValue("HWiNFO_" + metric + "_index", out v) && v != null) indexes[metric] = Convert.ToInt32(v, CultureInfo.InvariantCulture);
                }
                return SensorCheck.Evaluate(ReadGadget(), indexes);
            }
            catch (Exception) { return new List<string>(); }
        }

        private static IList<GadgetSensor> ReadGadget()
        {
            var list = new List<GadgetSensor>();
            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\HWiNFO64\VSB"))
            {
                if (key == null) return list;
                var numbers = key.GetValueNames().Where(n => n.StartsWith("Sensor", StringComparison.Ordinal)).Select(n => n.Substring(6)).Where(s => s.All(char.IsDigit)).Select(int.Parse).OrderBy(i => i);
                foreach (int i in numbers)
                    list.Add(new GadgetSensor { Index = i, Sensor = Text(key, "Sensor" + i), Label = Text(key, "Label" + i), Value = Text(key, "Value" + i) });
            }
            return list;
        }

        private static string Text(RegistryKey key, string name)
        {
            var v = key.GetValue(name);
            return v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture);
        }
    }
}
