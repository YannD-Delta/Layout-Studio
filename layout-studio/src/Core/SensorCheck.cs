using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>One value HWiNFO publishes in its gadget (registry SOFTWARE\HWiNFO64\VSB, entries SensorN, LabelN, ValueN).</summary>
    public class GadgetSensor
    {
        public int Index;
        public string Sensor;
        public string Label;
        public string Value;
    }

    /// <summary>
    /// GameActivity reads HWiNFO's gadget by position: each metric (fps, cpu, cpuT...) is an index into the gadget list. When the list
    /// changes, the indexes silently point at other sensors and the logged numbers are wrong. This compares what each index holds with
    /// what the metric expects, from the unit of the value. (PresentMon follows the foreground application, so a desktop compositor or another window there is normal outside a game and is not reported.)
    /// </summary>
    public static class SensorCheck
    {
        private static readonly string[][] Expected =
        {
            new[] { "fps", "FPS (framerate)", "FPS" },
            new[] { "cpu", "CPU usage", "%" },
            new[] { "cpuT", "CPU temperature", "°C" },
            new[] { "cpuP", "CPU power", "W" },
            new[] { "gpu", "GPU usage", "%" },
            new[] { "gpuT", "GPU temperature", "°C" },
            new[] { "gpuP", "GPU power", "W" },
            new[] { "ram", "RAM usage", "%" }
        };

        public static IList<string> Evaluate(IList<GadgetSensor> gadget, IDictionary<string, int> indexes)
        {
            var problems = new List<string>();
            if (gadget == null || indexes == null) return problems;
            if (gadget.Count == 0)
            {
                problems.Add("HWiNFO's gadget publishes no sensor: run HWiNFO and tick \"Report value in Gadget\" on the sensors GameActivity needs.");
                return problems;
            }
            foreach (var e in Expected)
            {
                int index;
                if (!indexes.TryGetValue(e[0], out index)) continue;
                string metric = e[1], unit = e[2];
                var sensor = gadget.FirstOrDefault(g => g.Index == index);
                if (sensor == null)
                {
                    problems.Add(metric + " reads gadget position " + index + ", but HWiNFO's gadget only lists " + gadget.Count + " sensors.");
                    continue;
                }
                string actual = UnitOf(sensor.Value);
                if (actual != unit)
                    problems.Add(metric + " reads gadget position " + index + " (" + Describe(sensor) + "), which is in " + (actual == "" ? "no unit" : actual) + ", not " + unit + ".");
            }
            return problems;
        }

        /// <summary>The unit after the number in a gadget value: "1,112 RPM" gives RPM, "45.0 °C" gives °C (a mangled degree sign is accepted).</summary>
        public static string UnitOf(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var text = value.Trim();
            int split = text.LastIndexOf(' ');
            if (split < 0) return "";
            string unit = text.Substring(split + 1).Trim();
            if (unit.Length > 0 && unit.Length <= 3 && (unit.EndsWith("C", StringComparison.Ordinal)) && unit.Length >= 2 && !char.IsLetterOrDigit(unit[0])) return "°C";
            if (unit == "°C") return "°C";
            return unit.Any(char.IsLetter) || unit == "%" ? unit : "";
        }

        private static string Describe(GadgetSensor s)
        {
            return ((s.Label ?? "").Trim() + (string.IsNullOrWhiteSpace(s.Sensor) ? "" : " of " + s.Sensor.Trim())).Trim();
        }
    }
}
