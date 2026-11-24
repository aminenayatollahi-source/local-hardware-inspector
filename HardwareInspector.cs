using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LocalHardwareInspector
{
    public sealed class DeviceList<T>
    {
        public bool Available { get; set; }
        public List<T> Devices { get; set; }
        public DeviceList() { Devices = new List<T>(); }
    }
    public sealed class Cpu
    {
        public string Model { get; set; }
        public uint? Cores { get; set; }
        public uint? LogicalProcessors { get; set; }
    }
    public sealed class Gpu
    {
        public string Model { get; set; }
        public string DriverVersion { get; set; }
    }
    public sealed class Snapshot
    {
        public string CapturedUtc { get; set; }
        public DeviceList<Cpu> Processors { get; set; }
        public DeviceList<Gpu> DisplayAdapters { get; set; }
        public ulong? TotalPhysicalBytes { get; set; }
        public ulong? AvailablePhysicalBytes { get; set; }
    }

    internal static class Reader
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length, Load;
            public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
                TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
        }
        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus value);

        private static DeviceList<T> Query<T>(string wql, Func<ManagementObject,T> map)
        {
            var result = new DeviceList<T>();
            try
            {
                var options = new EnumerationOptions();
                options.Timeout = TimeSpan.FromSeconds(12);
                using (var query = new ManagementObjectSearcher(@"root\cimv2", wql, options))
                using (var rows = query.Get())
                    foreach (ManagementObject row in rows)
                        using (row) result.Devices.Add(map(row));
                result.Available = true;
            }
            catch (ManagementException) { result.Devices.Clear(); }
            catch (UnauthorizedAccessException) { result.Devices.Clear(); }
            catch (COMException) { result.Devices.Clear(); }
            return result;
        }
        private static string Text(object value)
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
            return text.Length == 0 ? null : text;
        }
        private static uint? Count(object value)
        {
            uint n;
            return uint.TryParse(Text(value), out n) && n > 0 ? (uint?)n : null;
        }
        public static Snapshot Capture()
        {
            var s = new Snapshot();
            s.Processors = Query("SELECT Name,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor",
                row => new Cpu { Model = Text(row["Name"]), Cores = Count(row["NumberOfCores"]),
                                 LogicalProcessors = Count(row["NumberOfLogicalProcessors"]) });
            s.DisplayAdapters = Query("SELECT Name,DriverVersion FROM Win32_VideoController",
                row => new Gpu { Model = Text(row["Name"]), DriverVersion = Text(row["DriverVersion"]) });
            var memory = new MemoryStatus();
            memory.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            if (GlobalMemoryStatusEx(ref memory))
            {
                s.TotalPhysicalBytes = memory.TotalPhysical;
                s.AvailablePhysicalBytes = memory.AvailablePhysical;
            }
            s.CapturedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            return s;
        }
    }

    internal static class Display
    {
        private static string Value(object v) { return v == null ? "Unknown" : v.ToString(); }
        private static string GiB(ulong? bytes)
        {
            return bytes.HasValue ? (bytes.Value / 1073741824.0).ToString("0.00", CultureInfo.InvariantCulture) + " GiB" : "Unknown";
        }
        private static void Status<T>(StringBuilder b, DeviceList<T> items)
        {
            if (!items.Available) b.AppendLine("  Unknown (Windows query unavailable)");
            else if (items.Devices.Count == 0) b.AppendLine("  No devices reported by Windows");
        }
        public static string Format(Snapshot s)
        {
            var b = new StringBuilder();
            b.AppendLine("Processors");
            Status(b, s.Processors);
            foreach (var cpu in s.Processors.Devices)
            {
                b.AppendLine("  " + Value(cpu.Model));
                b.AppendLine("  Cores: " + Value(cpu.Cores) + "    Logical processors: " + Value(cpu.LogicalProcessors));
            }
            b.AppendLine().AppendLine("Physical memory");
            b.AppendLine("  Available to Windows (total): " + GiB(s.TotalPhysicalBytes));
            b.AppendLine("  Available now: " + GiB(s.AvailablePhysicalBytes));
            b.AppendLine().AppendLine("Display adapters");
            Status(b, s.DisplayAdapters);
            foreach (var gpu in s.DisplayAdapters.Devices)
                b.AppendLine("  " + Value(gpu.Model) + "    Driver: " + Value(gpu.DriverVersion));
            b.AppendLine().Append("Captured (UTC): ").Append(s.CapturedUtc);
            return b.ToString();
        }
    }

    internal sealed class MainWindow : Form
    {
        private readonly TextBox output = new TextBox();
        private readonly Button refresh = new Button();
        public MainWindow()
        {
            Text = "Local Hardware Inspector";
            ClientSize = new Size(725, 380);
            MinimumSize = new Size(430, 280);
            StartPosition = FormStartPosition.CenterScreen;
            output.Multiline = true;
            output.ReadOnly = true;
            output.ScrollBars = ScrollBars.Vertical;
            output.Dock = DockStyle.Fill;
            output.Font = new Font("Segoe UI", 10);
            refresh.Text = "Refresh";
            refresh.Dock = DockStyle.Bottom;
            refresh.Height = 34;
            Controls.Add(output);
            Controls.Add(refresh);
            Shown += (sender, args) => ReadHardware();
            refresh.Click += (sender, args) => ReadHardware();
        }
        private async void ReadHardware()
        {
            refresh.Enabled = false;
            output.Text = "Reading local hardware...";
            try
            {
                var snapshot = await Task.Run((Func<Snapshot>)Reader.Capture);
                if (!IsDisposed) output.Text = Display.Format(snapshot);
            }
            catch (Exception ex)
            {
                if (!IsDisposed) output.Text = "Could not complete the hardware report (" + ex.GetType().Name + ").";
            }
            finally { if (!IsDisposed) refresh.Enabled = true; }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainWindow());
                return 0;
            }
            if (args.Length != 2 || args[0] != "--json") return 2;
            try
            {
                string json = new JavaScriptSerializer().Serialize(Reader.Capture());
                using (var stream = new FileStream(args[1], FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.WriteLine(json);
                return 0;
            }
            catch (Exception) { return 1; }
        }
    }
}
