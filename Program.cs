using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
    private readonly Label adStatus = new() { AutoSize = true, Text = "Not checked", Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly Label adFingerprint = new() { AutoSize = true, Text = "Fingerprint: not available" };
    private readonly Label compareStatus = new() { AutoSize = true, Text = "Capture a baseline before resetting." };
    private readonly TextBox qaIdBox = new() { ReadOnly = true, Width = 430 };
    private readonly List<AppEntry> entries = new();
    private string? baselineFingerprint;
    private string? currentFingerprint;

    public MainForm()
    {
        Text = "Refresh — Windows Rewarded Ads QA";
        Width = 1120; Height = 760; MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(layout);

        layout.Controls.Add(new Label { Text = "Refresh", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 22, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(new Label { Text = "Windows Advertising ID QA  •  Application inspector", Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleRight }, 1, 0);

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

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var inspectTab = new TabPage("Application diagnostics");
        inspectTab.Controls.Add(details);
        var advertisingTab = new TabPage("Advertising ID QA");
        advertisingTab.Controls.Add(BuildAdvertisingPanel());
        tabs.TabPages.Add(inspectTab);
        tabs.TabPages.Add(advertisingTab);
        layout.Controls.Add(tabs, 1, 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        var inspect = new Button { Text = "Inspect selected app", AutoSize = true, Height = 36 };
        inspect.Click += (_, _) => { InspectSelected(); tabs.SelectedTab = inspectTab; };
        var settings = new Button { Text = "Open Windows advertising settings", AutoSize = true, Height = 36 };
        settings.Click += (_, _) => OpenAdvertisingSettings();
        var export = new Button { Text = "Export report…", AutoSize = true, Height = 36 };
        export.Click += (_, _) => ExportReport();
        actions.Controls.Add(inspect); actions.Controls.Add(settings); actions.Controls.Add(export);
        layout.Controls.Add(actions, 0, 2); layout.SetColumnSpan(actions, 2);

        var footer = new Label { Text = "Windows Advertising ID is user/device-wide. Refresh does not inject into other processes or override ad SDK controls.", Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft };
        layout.Controls.Add(footer, 0, 3); layout.SetColumnSpan(footer, 2);

        appList.SelectedIndexChanged += (_, _) => InspectSelected();
        LoadApplications();
        details.Text = "Select an application to inspect its package identity and running status.\r\n\r\nUse the Advertising ID QA tab to read the Windows ID, capture a baseline fingerprint, perform the reset yourself in Windows Settings, and verify whether Windows generated a different ID.\r\n\r\nA changed Windows ID does not prove that a running third-party app's private advertising SDK has reloaded its cached value.";
        var strip = new StatusStrip();
        strip.Items.Add(new ToolStripStatusLabel { Text = "Ready" });
        Controls.Add(strip);
    }

    private Control BuildAdvertisingPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12), ColumnCount = 1, RowCount = 10 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = "Windows Advertising ID — read-only diagnostics", AutoSize = true, Font = new Font("Segoe UI", 13, FontStyle.Bold) });
        panel.Controls.Add(new Label { Text = "Windows exposes this identifier as a read-only API. Refresh can inspect it and verify a Windows-generated reset; it cannot assign an arbitrary value.", AutoSize = true, MaximumSize = new Size(620, 0) });
        panel.Controls.Add(adStatus);
        panel.Controls.Add(adFingerprint);
        var read = new Button { Text = "Read current Windows Advertising ID", AutoSize = true };
        read.Click += (_, _) => ReadAndDisplayAdvertisingId();
        panel.Controls.Add(read);
        var capture = new Button { Text = "Capture current value as baseline", AutoSize = true };
        capture.Click += (_, _) => CaptureBaseline();
        panel.Controls.Add(capture);
        var open = new Button { Text = "Open Windows privacy settings", AutoSize = true };
        open.Click += (_, _) => OpenAdvertisingSettings();
        panel.Controls.Add(open);
        var verify = new Button { Text = "Read again and compare with baseline", AutoSize = true };
        verify.Click += (_, _) => VerifyAdvertisingId();
        panel.Controls.Add(verify);
        panel.Controls.Add(compareStatus);

        panel.Controls.Add(new Label { Text = "Local QA test identifier (not the Windows ID and not injected into other apps)", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(0, 14, 0, 0) });
        var qaRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
        qaIdBox.Width = 410;
        var generate = new Button { Text = "Generate test ID", AutoSize = true };
        generate.Click += (_, _) => qaIdBox.Text = Guid.NewGuid().ToString("D");
        var copy = new Button { Text = "Copy", AutoSize = true };
        copy.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(qaIdBox.Text)) Clipboard.SetText(qaIdBox.Text); };
        qaRow.Controls.Add(qaIdBox); qaRow.Controls.Add(generate); qaRow.Controls.Add(copy);
        panel.Controls.Add(qaRow);

        panel.Controls.Add(new Label { Text = "Reset procedure: capture baseline → open Settings → turn the advertising ID option off, then on again → return here and verify. Turning it off/on resets the Windows ID. This affects the Windows user/device context and may affect multiple apps. It does not force an arbitrary game's SDK to reload while it remains open.", AutoSize = true, MaximumSize = new Size(620, 0), ForeColor = Color.DarkSlateGray, Padding = new Padding(0, 12, 0, 0) });
        return new Panel { Dock = DockStyle.Fill, AutoScroll = true, Controls = { panel } };
    }

    private static string Fingerprint(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..12];
    }

    private static string ReadWindowsAdvertisingId()
    {
        const string script = "$ErrorActionPreference='Stop'; $id = [Windows.System.UserProfile.AdvertisingManager, Windows.System.UserProfile, ContentType=WindowsRuntime]::AdvertisingId; if ($null -eq $id) { [Console]::WriteLine(''); } else { [Console]::WriteLine([string]$id); }";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -EncodedCommand {encoded}")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start Windows PowerShell.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        if (!process.WaitForExit(10000))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("Windows Advertising ID query timed out.");
        }
        if (process.ExitCode != 0) throw new InvalidOperationException("Windows did not allow the AdvertisingManager API query. Check Windows version and app permissions.");
        return output;
    }

    private void ReadAndDisplayAdvertisingId()
    {
        try
        {
            var id = ReadWindowsAdvertisingId();
            if (string.IsNullOrWhiteSpace(id))
            {
                currentFingerprint = null;
                adStatus.Text = "Status: disabled or unavailable (Windows returned an empty ID)";
                adFingerprint.Text = "Fingerprint: unavailable";
                compareStatus.Text = "No identifier was returned. Check the Windows advertising privacy setting.";
                return;
            }
            currentFingerprint = Fingerprint(id);
            adStatus.Text = "Status: enabled — Windows returned an advertising ID";
            adFingerprint.Text = $"Fingerprint: {currentFingerprint}  •  raw identifier hidden";
            compareStatus.Text = "Read completed. Capture this value as a baseline before resetting.";
        }
        catch (Exception ex)
        {
            adStatus.Text = "Status: unable to read";
            adFingerprint.Text = "Fingerprint: unavailable";
            compareStatus.Text = ex.Message;
            MessageBox.Show(ex.Message, "Advertising ID diagnostic", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CaptureBaseline()
    {
        ReadAndDisplayAdvertisingId();
        if (currentFingerprint == null) return;
        baselineFingerprint = currentFingerprint;
        compareStatus.Text = $"Baseline captured: {baselineFingerprint}. Now use Windows Settings to turn the option off and back on.";
    }

    private void VerifyAdvertisingId()
    {
        ReadAndDisplayAdvertisingId();
        if (currentFingerprint == null || baselineFingerprint == null) return;
        compareStatus.Text = currentFingerprint == baselineFingerprint
            ? "NO CHANGE DETECTED: Windows returned the same identifier fingerprint as the baseline."
            : $"CHANGE VERIFIED: Windows returned a different identifier fingerprint ({baselineFingerprint} → {currentFingerprint}).";
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
        sb.AppendLine("• Windows Advertising ID is a read-only Windows API value shared by apps for this user/device.");
        sb.AppendLine("• Refresh can compare Windows ID fingerprints before and after a user-initiated reset.");
        sb.AppendLine("• A changed Windows ID does not prove this app's private SDK has refreshed its cached identifier.");
        sb.AppendLine();
        sb.AppendLine("SUPPORTED NEXT STEP");
        sb.AppendLine("Use the Advertising ID QA tab to capture a baseline and verify the Windows-level reset. In-process SDK refresh requires a documented app/provider interface.");
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
