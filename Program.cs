using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Management;
using Microsoft.Win32;

namespace Refresh;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed record AppEntry(string Name, string Kind, string Identity, string Location, bool IsRunning)
{
    public override string ToString() => $"{(IsRunning ? "●" : "○")}  {Name}  [{Kind}]";
}

public sealed class MainForm : Form
{
    private readonly ListBox appList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox details = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9) };
    private readonly Label status = new() { AutoSize = true, Text = "Ready" };
    private readonly List<AppEntry> entries = new();

    public MainForm()
    {
        Text = "Refresh — Windows Rewarded Ads QA";
        Width = 1060; Height = 720; MinimumSize = new Size(850, 580);
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(layout);

        var heading = new Label { Text = "Refresh", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 22, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        layout.Controls.Add(heading, 0, 0);
        var subtitle = new Label { Text = "Windows Rewarded Ads QA  •  Standalone application inspector", Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleRight };
        layout.Controls.Add(subtitle, 1, 0);

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.Controls.Add(new Label { Text = "Installed Store apps and running apps", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        left.Controls.Add(appList, 0, 1);
        var reload = new Button { Text = "↻  Refresh application list", Dock = DockStyle.Fill };
        reload.Click += (_, _) => LoadApplications();
        left.Controls.Add(reload, 0, 2);
        layout.Controls.Add(left, 0, 1);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(new Label { Text = "Selected application diagnostics", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        right.Controls.Add(details, 0, 1);
        layout.Controls.Add(right, 1, 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        var inspect = new Button { Text = "Inspect selected app", AutoSize = true, Height = 36 };
        inspect.Click += (_, _) => InspectSelected();
        var settings = new Button { Text = "Open Windows advertising settings", AutoSize = true, Height = 36 };
        settings.Click += (_, _) => OpenAdvertisingSettings();
        var export = new Button { Text = "Export report…", AutoSize = true, Height = 36 };
        export.Click += (_, _) => ExportReport();
        actions.Controls.Add(inspect); actions.Controls.Add(settings); actions.Controls.Add(export);
        layout.Controls.Add(actions, 0, 2); layout.SetColumnSpan(actions, 2);

        var footer = new Label { Text = "Refresh does not inject into apps or override advertising SDK / ad-network controls.", Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft };
        layout.Controls.Add(footer, 0, 3); layout.SetColumnSpan(footer, 2);

        appList.SelectedIndexChanged += (_, _) => InspectSelected();
        LoadApplications();
        details.Text = "Welcome to Refresh.\r\n\r\nSelect an application to inspect its package identity and running status.\r\n\r\nUse “Open Windows advertising settings” to review Windows privacy controls.\r\n\r\nTechnical note: Windows does not expose a universal external mechanism to replace an ID already cached in another process or force an arbitrary closed-source ad SDK to reload a rewarded ad while it remains open.\r\n\r\nFor real rewarded-ad QA, use the advertising provider's official test mode and an application-supported ad reload mechanism.";
        var strip = new StatusStrip();
        strip.Items.Add(new ToolStripStatusLabel { Text = "Ready" });
        Controls.Add(strip);
    }

    private void LoadApplications()
    {
        entries.Clear();
        var running = Process.GetProcesses().Select(p => { try { return p.ProcessName; } catch { return ""; } }).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-AppxPackage | Select-Object Name,PackageFullName,InstallLocation | ConvertTo-Json -Compress\"")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var json = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(8000);
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
                var root = doc.RootElement;
                var packages = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToArray() : new[] { root };
                foreach (var item in packages)
                {
                    string Get(string key) => item.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
                    var name = Get("Name");
                    var full = Get("PackageFullName");
                    var location = Get("InstallLocation");
                    if (name.Length > 0)
                        entries.Add(new AppEntry(name, "Microsoft Store / AppX", full, location, running.Contains(name)));
                }
            }
        }
        catch { /* Store package discovery may be unavailable on managed Windows devices. */ }

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = p.MainModule?.FileName ?? "";
                if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    !entries.Any(x => x.Name.Equals(p.ProcessName, StringComparison.OrdinalIgnoreCase)))
                    entries.Add(new AppEntry(p.ProcessName, "Running desktop process", p.Id.ToString(), path, true));
            }
            catch { }
            finally { p.Dispose(); }
        }

        appList.BeginUpdate();
        appList.Items.Clear();
        foreach (var item in entries.OrderByDescending(x => x.IsRunning).ThenBy(x => x.Name))
            appList.Items.Add(item);
        appList.EndUpdate();
        status.Text = $"Found {entries.Count} applications";
        if (appList.Items.Count > 0) appList.SelectedIndex = 0;
    }

    private void InspectSelected()
    {
        if (appList.SelectedItem is not AppEntry app) return;
        var sb = new StringBuilder();
        sb.AppendLine("REFRESH — APPLICATION INSPECTION");
        sb.AppendLine($"Time: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"Computer: {Environment.MachineName}");
        sb.AppendLine($"Windows: {Environment.OSVersion}");
        sb.AppendLine();
        sb.AppendLine($"Application: {app.Name}");
        sb.AppendLine($"Type: {app.Kind}");
        sb.AppendLine($"Running: {(app.IsRunning ? "Yes (snapshot at last scan)" : "Not detected as running")}");
        sb.AppendLine($"Package / process identity: {app.Identity}");
        sb.AppendLine($"Install / executable location: {app.Location}");
        sb.AppendLine();
        sb.AppendLine("ADVERTISING QA STATUS");
        sb.AppendLine("• This utility cannot inspect private advertising SDK state inside the selected process.");
        sb.AppendLine("• A Windows Advertising ID reset is a Windows privacy setting, not a per-app ID override.");
        sb.AppendLine("• Refresh cannot force a new ad response or guarantee inventory.");
        sb.AppendLine();
        sb.AppendLine("SUPPORTED NEXT STEP");
        sb.AppendLine("Open Windows advertising settings to review the OS privacy control. For an in-process rewarded-ad reload, the application must expose a documented command or SDK-supported reload.");
        details.Text = sb.ToString();
    }

    private void OpenAdvertisingSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:privacy-general") { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show($"Could not open Windows Settings: {ex.Message}", "Refresh", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void ExportReport()
    {
        if (appList.SelectedItem is not AppEntry) { MessageBox.Show("Select an application first."); return; }
        InspectSelected();
        using var dialog = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = "Refresh_Application_Report.txt" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName, details.Text, Encoding.UTF8);
            MessageBox.Show("Report saved.", "Refresh", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
