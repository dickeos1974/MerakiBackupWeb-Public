using MerakiBackupWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace MerakiBackupWeb.Pages;

public class RestoreSsidModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly MerakiApiClient _merakiApiClient;

    public string StatusMessage { get; set; } = "";

    public List<string> BackupFolders { get; set; } = new();
    public List<string> SsidFiles { get; set; } = new();
    public List<Dictionary<string, object>> Ssids { get; set; } = new();
    public List<Dictionary<string, object>> Networks { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SelectedBackup { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedFile { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedNetworkId { get; set; }

    [BindProperty]
    public string? SsidNumber { get; set; }

    public async Task<IActionResult> OnPostRestore()
    {
        if (string.IsNullOrWhiteSpace(SelectedBackup) ||
            string.IsNullOrWhiteSpace(SelectedFile) ||
            string.IsNullOrWhiteSpace(SsidNumber))
        {
            return RedirectToPage();
        }

        var filePath = Path.Combine(GetBackupRoot(), SelectedBackup, SelectedFile);

        if (!System.IO.File.Exists(filePath))
            return RedirectToPage();

        var json = System.IO.File.ReadAllText(filePath);

        var ssids = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

        if (ssids == null)
            return RedirectToPage();
        //TAG check start
        var orgId = _configuration["Meraki:OrgId"];

        var hasTag = await _merakiApiClient.NetworkHasTagAsync(
            orgId,
            SelectedNetworkId!,
            "merakiRestore"
        );

        if (!hasTag)
        {
            StatusMessage = "Restore blocked: target network does not have 'merakiRestore' tag.";

            LoadBackupFolders();
            if (!string.IsNullOrWhiteSpace(SelectedBackup))
                LoadSsidFiles(SelectedBackup); // or LoadVlanFiles for VLAN page

            if (!string.IsNullOrWhiteSpace(SelectedBackup) &&
                !string.IsNullOrWhiteSpace(SelectedFile))
                LoadSsids(SelectedBackup, SelectedFile); // or LoadVlans

            await LoadNetworksAsync();

            return Page();
        }
        //TAG check end


        var ssid = ssids.FirstOrDefault(s =>
            s.ContainsKey("number") &&
            s["number"]?.ToString() == SsidNumber
        );

        if (ssid == null)
            return RedirectToPage();

        if (string.IsNullOrWhiteSpace(SelectedNetworkId))
        {
            StatusMessage = "Please select a target network.";

            LoadBackupFolders();

            if (!string.IsNullOrWhiteSpace(SelectedBackup))
                LoadSsidFiles(SelectedBackup);

            if (!string.IsNullOrWhiteSpace(SelectedBackup) &&
                !string.IsNullOrWhiteSpace(SelectedFile))
                LoadSsids(SelectedBackup, SelectedFile);

            await LoadNetworksAsync();

            return Page();
        }

        var targetNetworkId = SelectedNetworkId;

        // Build payload (full restore minus unsafe fields)
        // var payload = BuildSsidRestorePayload(ssid);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = ssid["name"],
            ["enabled"] = ssid["enabled"],
            ["visible"] = ssid["visible"],
            ["authMode"] = ssid["authMode"],
            ["encryptionMode"] = ssid["encryptionMode"],
            ["wpaEncryptionMode"] = ssid["wpaEncryptionMode"],
            ["ipAssignmentMode"] = ssid["ipAssignmentMode"]
        };

        //Radius and PSK fields require special handling due to potential security implications and API requirements
            if (ssid.ContainsKey("psk"))
            payload["psk"] = ssid["psk"];

        if (ssid["authMode"]?.ToString()?.Contains("radius") == true)
        {
            if (ssid.ContainsKey("radiusServers"))
                payload["radiusServers"] = ssid["radiusServers"];

            if (ssid.ContainsKey("radiusAccountingEnabled"))
                payload["radiusAccountingEnabled"] = ssid["radiusAccountingEnabled"];

            if (ssid.ContainsKey("radiusAccountingServers"))
                payload["radiusAccountingServers"] = ssid["radiusAccountingServers"];
        }

        await _merakiApiClient.UpdateSsidRawAsync(
         targetNetworkId,
         SsidNumber,
         payload
        );

        StatusMessage = $"SSID {SsidNumber} restored successfully.";

        LoadBackupFolders();
        LoadSsidFiles(SelectedBackup);
        LoadSsids(SelectedBackup, SelectedFile);
        await LoadNetworksAsync();

        StatusMessage = $"SSID {SsidNumber} restored successfully.";

        return Page();
    }

    public RestoreSsidModel(
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
        {
            LoadSsidFiles(SelectedBackup);
        }

        if (!string.IsNullOrWhiteSpace(SelectedBackup) &&
            !string.IsNullOrWhiteSpace(SelectedFile))
        {
            LoadSsids(SelectedBackup, SelectedFile);
        }

        await LoadNetworksAsync();
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

    private void LoadSsidFiles(string backupFolder)
    {
        var folderPath = Path.Combine(GetBackupRoot(), backupFolder);

        if (!Directory.Exists(folderPath))
            return;

        SsidFiles = Directory
            .GetFiles(folderPath, "ssids_*.json")
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x)
            .ToList()!;
    }

    private void LoadSsids(string backupFolder, string fileName)
    {
        var filePath = Path.Combine(GetBackupRoot(), backupFolder, fileName);

        if (!System.IO.File.Exists(filePath))
            return;

        var json = System.IO.File.ReadAllText(filePath);

        var ssids = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

        if (ssids != null)
            Ssids = ssids;
    }
    private async Task LoadNetworksAsync()
    {
        var orgId = _configuration["Meraki:OrgId"];

        if (string.IsNullOrWhiteSpace(orgId))
            return;

        var networksJson = await _merakiApiClient.GetNetworksRawAsync(orgId);

        var networks = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(networksJson);

        if (networks != null)
            Networks = networks;
    }
    private string GetBackupRoot()
    {
        var backupPath = _configuration["Backup:BackupPath"] ?? "Backups";

        return Path.Combine(
            _environment.ContentRootPath,
            backupPath
        );
    }

}