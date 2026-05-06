using System.Text.Json;
using MerakiBackupWeb.Models;

namespace MerakiBackupWeb.Services;

public class BackupRunResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string BackupFolder { get; set; } = "";

    public int NetworkCount { get; set; }
    public int SsidCount { get; set; }
    public int VlanCount { get; set; }
    public int FirewallCount { get; set; }
    public int SwitchPortCount { get; set; }
}

public class MerakiBackupRunner
{
    private readonly MerakiApiClient _merakiApiClient;
    private readonly BackupService _backupService;
    private readonly BackupProgress _progress;

    public MerakiBackupRunner(
        MerakiApiClient merakiApiClient,
        BackupService backupService,
        BackupProgress progress)
    {
        _merakiApiClient = merakiApiClient;
        _backupService = backupService;
        _progress = progress;
    }

    public async Task<BackupRunResult> RunBackupAsync()
    {
        var result = new BackupRunResult();
        var logLines = new List<string>();
        string backupFolder = "";

        void Log(string message)
        {
            var line = $"{DateTime.Now:HH:mm:ss} - {message}";
            logLines.Add(line);
            _progress.Status = message;
        }

        try
        {
            Log("Backup started");

            backupFolder = _backupService.CreateBackupRunFolder();
            result.BackupFolder = backupFolder;

            Log("Backup folder created");
            Log("Getting organisations...");

            var orgJson = await _merakiApiClient.GetOrganizationsRawAsync();

            await _backupService.SaveBackupAsync(
                backupFolder,
                "organizations.json",
                orgJson
            );

            var orgs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(orgJson);

            if (orgs == null || orgs.Count == 0)
                throw new Exception("No organisations found");

            foreach (var org in orgs)
            {
                var orgId = org["id"]?.ToString();
                var orgName = MakeSafeFileName(org["name"]?.ToString() ?? orgId!);

                if (string.IsNullOrWhiteSpace(orgId))
                    continue;

                Log($"Getting networks for {orgName}");

                var networksJson = await _merakiApiClient.GetNetworksRawAsync(orgId);

                await _backupService.SaveBackupAsync(
                    backupFolder,
                    $"networks_{orgName}.json",
                    networksJson
                );

                var networks = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(networksJson);

                if (networks == null)
                    continue;

                foreach (var net in networks)
                {
                    var netId = net["id"]?.ToString();
                    var netName = MakeSafeFileName(net["name"]?.ToString() ?? netId!);

                    if (string.IsNullOrWhiteSpace(netId))
                        continue;

                    result.NetworkCount++;

                    Log($"Processing network {result.NetworkCount}: {netName}");

                    try
                    {
                        Log($"Getting SSIDs for {netName}");

                        var ssidJson = await _merakiApiClient.GetSsidsRawAsync(netId);

                        await _backupService.SaveBackupAsync(
                            backupFolder,
                            $"ssids_{orgName}_{netName}.json",
                            ssidJson
                        );

                        result.SsidCount++;
                        Log($"SSIDs saved for {netName}");
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("400") || ex.Message.Contains("404"))
                            Log($"Skipped SSIDs for {netName} (not applicable)");
                        else
                            Log($"ERROR SSIDs {netName}: {ex.Message}");
                    }

                    try
                    {
                        Log($"Getting VLANs for {netName}");

                        var vlanJson = await _merakiApiClient.GetVlansRawAsync(netId);

                        await _backupService.SaveBackupAsync(
                            backupFolder,
                            $"vlans_{orgName}_{netName}.json",
                            vlanJson
                        );

                        result.VlanCount++;
                        Log($"VLANs saved for {netName}");
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("400") || ex.Message.Contains("404"))
                            Log($"Skipped VLANs for {netName} (not applicable)");
                        else
                            Log($"ERROR VLANs {netName}: {ex.Message}");
                    }

                    try
                    {
                        Log($"Getting firewall rules for {netName}");

                        var fwJson = await _merakiApiClient.GetL3FirewallRulesRawAsync(netId);

                        await _backupService.SaveBackupAsync(
                            backupFolder,
                            $"firewall_{orgName}_{netName}.json",
                            fwJson
                        );

                        result.FirewallCount++;
                        Log($"Firewall rules saved for {netName}");
                    }
                    catch (Exception ex)
                    {
                        Log($"ERROR firewall {netName}: {ex.Message}");
                    }

                    try
                    {
                        Log($"Getting devices for {netName}");

                        var devicesJson = await _merakiApiClient.GetDevicesRawAsync(netId);

                        await _backupService.SaveBackupAsync(
                            backupFolder,
                            $"devices_{orgName}_{netName}.json",
                            devicesJson
                        );

                        var devices = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(devicesJson);

                        if (devices != null)
                        {
                            foreach (var device in devices)
                            {
                                var serial = device["serial"]?.ToString();
                                var model = device["model"]?.ToString();

                                if (string.IsNullOrWhiteSpace(serial) || string.IsNullOrWhiteSpace(model))
                                    continue;

                                if (!model.StartsWith("MS"))
                                    continue;

                                Log($"Getting switch ports for {netName} / {serial}");

                                var switchPortsJson = await _merakiApiClient.GetSwitchPortsRawAsync(serial);

                                await _backupService.SaveBackupAsync(
                                    backupFolder,
                                    $"switch_ports_{orgName}_{netName}_{serial}.json",
                                    switchPortsJson
                                );

                                result.SwitchPortCount++;
                                Log($"Switch ports saved for {serial}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"ERROR switch ports {netName}: {ex.Message}");
                    }
                }
            }

            Log("Backup complete");

            await File.WriteAllLinesAsync(
                Path.Combine(backupFolder, "log.txt"),
                logLines
            );

            var backupRoot = Path.GetDirectoryName(backupFolder)!;
            DeleteOldBackups(backupRoot);

            result.Success = true;
            result.Message =
                $"Backup complete: {result.NetworkCount} networks, {result.SsidCount} SSID configs, {result.VlanCount} VLAN configs, {result.FirewallCount} firewall configs, {result.SwitchPortCount} switch port configs saved";

            return result;
        }
        catch (Exception ex)
        {
            Log($"Backup failed: {ex.Message}");

            if (!string.IsNullOrWhiteSpace(backupFolder))
            {
                await File.WriteAllLinesAsync(
                    Path.Combine(backupFolder, "log.txt"),
                    logLines
                );
            }

            result.Success = false;
            result.Message = $"Error: {ex.Message}";
            result.BackupFolder = backupFolder;

            return result;
        }
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Replace(" ", "_");
    }
    private void DeleteOldBackups(string backupRoot)
    {
        if (!Directory.Exists(backupRoot))
            return;

        var cutoff = DateTime.Now.AddDays(-30);

        foreach (var folder in Directory.GetDirectories(backupRoot))
        {
            try
            {
                var created = Directory.GetCreationTime(folder);

                if (created < cutoff)
                {
                    Directory.Delete(folder, true);
                }
            }
            catch
            {
                // ignore errors (locked files etc.)
            }
        }
    }
}