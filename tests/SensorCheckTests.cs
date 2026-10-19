using System.Collections.Generic;
using System.Linq;
using LayoutStudio.Core;

public static class SensorCheckTests
{
    // The gadget list of the owner's HWiNFO when this was written: only six sensors, in this order.
    private static IList<GadgetSensor> OwnerGadget()
    {
        return new List<GadgetSensor>
        {
            new GadgetSensor { Index = 0, Sensor = "CPU [#0]: AMD Ryzen 7 5800X3D: Enhanced", Label = "CPU Package Power", Value = "49.592 W" },
            new GadgetSensor { Index = 1, Sensor = "GIGABYTE X570 AORUS ELITE (ITE IT8688E)", Label = "CPU", Value = "1,112 RPM" },
            new GadgetSensor { Index = 2, Sensor = "GPU [#0]: AMD Radeon RX 7900 XTX", Label = "GPU Temperature", Value = "45.0 °C" },
            new GadgetSensor { Index = 3, Sensor = "GPU [#0]: AMD Radeon RX 7900 XTX", Label = "GPU Power Maximum", Value = "86.645 W" },
            new GadgetSensor { Index = 4, Sensor = "GPU [#0]: AMD Radeon RX 7900 XTX", Label = "GPU Utilization", Value = "30.0 %" },
            new GadgetSensor { Index = 5, Sensor = "PresentMon [dwm.exe]", Label = "Framerate Displayed (avg)", Value = "160.0 FPS" }
        };
    }

    private static IDictionary<string, int> OwnerMapping()
    {
        return new Dictionary<string, int> { { "fps", 5 }, { "cpu", 0 }, { "cpuT", 1 }, { "cpuP", 0 }, { "gpu", 4 }, { "gpuT", 2 }, { "gpuP", 3 }, { "ram", 0 } };
    }

    public static void A_fan_read_as_a_temperature_is_reported()
    {
        var problems = SensorCheck.Evaluate(OwnerGadget(), OwnerMapping());
        Assert.True(problems.Any(p => p.Contains("CPU temperature") && p.Contains("RPM")), string.Join(" | ", problems));
    }

    public static void A_power_sensor_read_as_usage_is_reported_for_cpu_and_ram()
    {
        var problems = SensorCheck.Evaluate(OwnerGadget(), OwnerMapping());
        Assert.True(problems.Any(p => p.Contains("CPU usage") && p.Contains(" W")), string.Join(" | ", problems));
        Assert.True(problems.Any(p => p.Contains("RAM usage") && p.Contains(" W")), string.Join(" | ", problems));
    }

    public static void A_framerate_following_the_desktop_outside_a_game_is_not_an_error()
    {
        var problems = SensorCheck.Evaluate(OwnerGadget(), OwnerMapping());
        Assert.False(problems.Any(p => p.Contains("dwm.exe") || p.Contains("FPS")), string.Join(" | ", problems));
    }

    public static void Correct_sensors_raise_nothing()
    {
        var gadget = new List<GadgetSensor>
        {
            new GadgetSensor { Index = 0, Sensor = "CPU", Label = "Total CPU Usage", Value = "19.0 %" },
            new GadgetSensor { Index = 1, Sensor = "CPU", Label = "CPU (Tctl/Tdie)", Value = "61.0 °C" },
            new GadgetSensor { Index = 2, Sensor = "CPU", Label = "CPU Package Power", Value = "49.6 W" },
            new GadgetSensor { Index = 3, Sensor = "GPU", Label = "GPU Utilization", Value = "30.0 %" },
            new GadgetSensor { Index = 4, Sensor = "GPU", Label = "GPU Temperature", Value = "45.0 °C" },
            new GadgetSensor { Index = 5, Sensor = "GPU", Label = "GPU Power", Value = "86.6 W" },
            new GadgetSensor { Index = 6, Sensor = "Memory", Label = "Physical Memory Load", Value = "52.0 %" },
            new GadgetSensor { Index = 7, Sensor = "PresentMon [eldenring.exe]", Label = "Framerate Displayed (avg)", Value = "90.0 FPS" }
        };
        var map = new Dictionary<string, int> { { "fps", 7 }, { "cpu", 0 }, { "cpuT", 1 }, { "cpuP", 2 }, { "gpu", 3 }, { "gpuT", 4 }, { "gpuP", 5 }, { "ram", 6 } };
        Assert.Equal(0, SensorCheck.Evaluate(gadget, map).Count);
    }

    public static void An_index_past_the_end_of_the_list_is_reported()
    {
        var gadget = OwnerGadget();
        var map = OwnerMapping();
        map["ram"] = 9;
        var problems = SensorCheck.Evaluate(gadget, map);
        Assert.True(problems.Any(p => p.Contains("RAM usage") && p.Contains("only lists 6")), string.Join(" | ", problems));
    }

    public static void An_empty_gadget_list_says_so_once()
    {
        var problems = SensorCheck.Evaluate(new List<GadgetSensor>(), OwnerMapping());
        Assert.Equal(1, problems.Count);
        Assert.True(problems[0].Contains("no sensor"));
    }

    public static void Units_are_read_from_the_value_text_in_any_number_format()
    {
        Assert.Equal("RPM", SensorCheck.UnitOf("1,112 RPM"));
        Assert.Equal("W", SensorCheck.UnitOf("49.592 W"));
        Assert.Equal("%", SensorCheck.UnitOf("30,0 %"));
        Assert.Equal("°C", SensorCheck.UnitOf("45.0 °C"));
        Assert.Equal("°C", SensorCheck.UnitOf("45.0 �C"), "a mangled degree sign still reads as a Celsius temperature");
        Assert.Equal("FPS", SensorCheck.UnitOf("160.0 FPS"));
        Assert.Equal("", SensorCheck.UnitOf("n/a"));
    }
}
