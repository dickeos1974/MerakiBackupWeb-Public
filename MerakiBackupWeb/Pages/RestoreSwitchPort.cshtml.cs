using MerakiBackupWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace MerakiBackupWeb.Pages;

public class RestoreSwitchPortModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly MerakiApiClient _merakiApiClient;

    public string StatusMessage { get; set; } = "";
    public List<string> BackupFolders { get; set; } = new();
    public List<string> SwitchPortFiles { get; set; } = new();
    public List<Dictionary<string, object>> Ports { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SelectedBackup { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedFile { get; set; }
    [BindProperty]
    public string? PortId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedSerial { get; set; }

    [BindProperty]
    public bool ConfirmRestore { get; set; }

    public List<Dictionary<string, object>> Devices { get; set; } = new();
    public RestoreSwitchPortModel(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        MerakiApiClient merakiApiClient)
    {
        _environment = environment;
        _configuration = configuration;
        _merakiApiClient = merakiApiClient;
    }

    public async Task OnGetAsync()
    {
        LoadBackupFolders();

        if (!string.IsNullOrWhiteSpace(SelectedBackup))
            LoadSwitchPortFiles(SelectedBackup);

        if (!string.IsNullOrWhiteSpace(SelectedBackup) &&
            !string.IsNullOrWhiteSpace(SelectedFile))
            LoadPorts(SelectedBackup, SelectedFile);
        await LoadDevicesAsync();
    }

    private void LoadBackupFolders()
    {
        var backupRoot = GetBackupRoot();

        if (!Directory.Exists(backupRoot))
            return;

        BackupFolders = Directory
            .GetDirectories(backupRoot)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderByDescending(x => x)
            .ToList()!;
    }

    private void LoadSwitchPortFiles(string backupFolder)
    {
        var folderPath = Path.Combine(GetBackupRoot(), backupFolder);

        if (!Directory.Exists(folderPath))
            return;

        SwitchPortFiles = Directory
            .GetFiles(folderPath, "switch_ports_*.json")
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x)
            .ToList()!;
    }

    private void LoadPorts(string backupFolder, string fileName)
    {
        var filePath = Path.Combine(GetBackupRoot(), backupFolder, fileName);

        if (!System.IO.File.Exists(filePath))
            return;

        var json = System.IO.File.ReadAllText(filePath);

        var ports = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

        if (ports != null)
            Ports = ports;
    }

    private string GetBackupRoot()
    {
        var backupPath = _configuration["Backup:BackupPath"] ?? "Backups";

        return Path.Combine(
            _environment.ContentRootPath,
            backupPath
        );
    }
    private async Task LoadDevicesAsync()
    {
        var orgId = _configuration["Meraki:OrgId"];

        var networksJson = await _merakiApiClient.GetNetworksRawAsync(orgId);

        var networks = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(networksJson);

        var devices = new List<Dictionary<string, object>>();

        if (networks != null)
        {
            foreach (var net in networks)
            {
                var networkId = net["id"]?.ToString();

                if (string.IsNullOrWhiteSpace(networkId))
                    continue;

                try
                {
                    var devicesJson = await _merakiApiClient.GetDevicesRawAsync(networkId);

                    var devs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(devicesJson);

                    if (devs != null)
                    {
                        foreach (var d in devs)
                        {
                            if (d.ContainsKey("model") &&
                                d["model"]?.ToString()?.StartsWith("MS") == true)
                            {
                                devices.Add(d);
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        Devices = devices;
    }
    public async Task<IActionResult> OnPostRestore()
    {
        await Reload();

        if (string.IsNullOrWhiteSpace(SelectedSerial))
        {
            StatusMessage = "Please select a target switch.";
            return Page();
        }

        if (!ConfirmRestore)
        {
            StatusMessage = "Please confirm restore.";
            return Page();
        }

        var orgId = _configuration["Meraki:OrgId"];

        var device = Devices.FirstOrDefault(d =>
            d["serial"]?.ToString() == SelectedSerial
        );

        var networkId = device?["networkId"]?.ToString();

        if (string.IsNullOrWhiteSpace(networkId))
        {
            StatusMessage = "Unable to determine device network.";
            return Page();
        }

        var hasTag = await _merakiApiClient.NetworkHasTagAsync(
            orgId!,
            networkId,
            "merakiRestore"
        );

        if (!hasTag)
        {
            StatusMessage = "Restore blocked: network not tagged.";
            return Page();
        }

        var filePath = Path.Combine(GetBackupRoot(), SelectedBackup!, SelectedFile!);
        var json = System.IO.File.ReadAllText(filePath);

        var ports = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

        var port = ports?.FirstOrDefault(p =>
            p["portId"]?.ToString() == PortId
        );

        if (port == null)
        {
            StatusMessage = "Port not found in backup.";
            return Page();
        }

        // 🚫 Block uplinks (very important)
        if (port.ContainsKey("tags") && port["tags"]?.ToString()?.Contains("uplink") == true)
        {
            StatusMessage = "Blocked: uplink ports cannot be restored.";
            return Page();
        }

        var payload = new Dictionary<string, object?>
        {
            ["name"] = port["name"],
            ["enabled"] = port["enabled"],
            ["type"] = port["type"],
            ["vlan"] = port["vlan"],
            ["voiceVlan"] = port["voiceVlan"],
            ["allowedVlans"] = port["allowedVlans"],
            ["poeEnabled"] = port["poeEnabled"]
        };

        await _merakiApiClient.UpdateSwitchPortAsync(
            SelectedSerial,
            PortId!,
            payload
        );

        StatusMessage = $"Port {PortId} restored successfully.";

        return Page();
    }
    private async Task Reload()
    {
        LoadBackupFolders();

        if (!string.IsNullOrWhiteSpace(SelectedBackup))
            LoadSwitchPortFiles(SelectedBackup);

        if (!string.IsNullOrWhiteSpace(SelectedBackup) &&
            !string.IsNullOrWhiteSpace(SelectedFile))
            LoadPorts(SelectedBackup, SelectedFile);

        await LoadDevicesAsync();
    }
}