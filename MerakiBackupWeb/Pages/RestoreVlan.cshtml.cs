using MerakiBackupWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace MerakiBackupWeb.Pages;

public class RestoreVlanModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly MerakiApiClient _merakiApiClient;

    public string StatusMessage { get; set; } = "";
    public string PreviewMessage { get; set; } = "";

    public List<string> BackupFolders { get; set; } = new();
    public List<string> VlanFiles { get; set; } = new();
    public List<Dictionary<string, object>> Vlans { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SelectedBackup { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedFile { get; set; }
    [BindProperty]

    public string? VlanId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedNetworkId { get; set; }

    [BindProperty]
    public bool ConfirmRestore { get; set; }
    public List<Dictionary<string, object>> Networks { get; set; } = new();

    public RestoreVlanModel(
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
            LoadVlanFiles(SelectedBackup);

        if (!string.IsNullOrWhiteSpace(SelectedBackup) &&
            !string.IsNullOrWhiteSpace(SelectedFile))
            LoadVlans(SelectedBackup, SelectedFile);

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

    private void LoadVlanFiles(string backupFolder)
    {
        var folderPath = Path.Combine(GetBackupRoot(), backupFolder);

        if (!Directory.Exists(folderPath))
            return;

        VlanFiles = Directory
            .GetFiles(folderPath, "vlans_*.json")
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x)
            .ToList()!;
    }

    private void LoadVlans(string backupFolder, string fileName)
    {
        var filePath = Path.Combine(GetBackupRoot(), backupFolder, fileName);

        if (!System.IO.File.Exists(filePath))
            return;

        var json = System.IO.File.ReadAllText(filePath);

        var vlans = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

        if (vlans != null)
            Vlans = vlans;
    }

    private string GetBackupRoot()
    {
        var backupPath = _configuration["Backup:BackupPath"] ?? "Backups";

        return Path.Combine(
            _environment.ContentRootPath,
            backupPath
        );
    }
    private async Task LoadNetworksAsync()
    {
        var orgId = _configuration["Meraki:OrgId"];

        var networksJson = await _merakiApiClient.GetNetworksRawAsync(orgId);

        var networks = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(networksJson);

        if (networks != null)
            Networks = networks;
    }
    public async Task<IActionResult> OnPostRestore()
    {
        if (string.IsNullOrWhiteSpace(SelectedNetworkId))
        {
            StatusMessage = "Please select a target network.";

            LoadBackupFolders();
            if (!string.IsNullOrWhiteSpace(SelectedBackup))
                LoadVlanFiles(SelectedBackup);
            if (!string.IsNullOrWhiteSpace(SelectedBackup) && !string.IsNullOrWhiteSpace(SelectedFile))
                LoadVlans(SelectedBackup, SelectedFile);

            await LoadNetworksAsync();
            return Page();
        }

        if (!ConfirmRestore)
        {
            StatusMessage = "Please confirm restore before proceeding.";

            LoadBackupFolders();
            if (!string.IsNullOrWhiteSpace(SelectedBackup))
                LoadVlanFiles(SelectedBackup);
            if (!string.IsNullOrWhiteSpace(SelectedBackup) && !string.IsNullOrWhiteSpace(SelectedFile))
                LoadVlans(SelectedBackup, SelectedFile);

            await LoadNetworksAsync();

            return Page();
        }

        var filePath = Path.Combine(GetBackupRoot(), SelectedBackup!, SelectedFile!);

        var json = System.IO.File.ReadAllText(filePath);

        var vlans = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

        var vlan = vlans?.FirstOrDefault(v =>
            v.ContainsKey("id") &&
            v["id"]?.ToString() == VlanId
        );

        if (vlan == null)
            return Page();
        //Tag check Start
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
                LoadVlanFiles(SelectedBackup); // or LoadVlanFiles for VLAN page

            if (!string.IsNullOrWhiteSpace(SelectedBackup) &&
                !string.IsNullOrWhiteSpace(SelectedFile))
                LoadVlans(SelectedBackup, SelectedFile); // or LoadVlans
            await LoadNetworksAsync();

            return Page();
        }
        //Tag Check End

        var payload = new Dictionary<string, object?>
        {
            ["name"] = vlan["name"],
            ["subnet"] = vlan["subnet"],
            ["applianceIp"] = vlan["applianceIp"]
        };

        var existingVlansJson = await _merakiApiClient.GetVlansRawAsync(SelectedNetworkId);

        var existingVlans = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(existingVlansJson);

        var vlanExists = existingVlans?.Any(v =>
            v.ContainsKey("id") &&
            v["id"]?.ToString() == VlanId
        ) == true;

        if (vlanExists)
        {
            await _merakiApiClient.UpdateVlanAsync(
                SelectedNetworkId,
                VlanId!,
                payload
            );
        }
        else
        {
            // Add VLAN ID into payload for create
            payload["id"] = VlanId;

            await _merakiApiClient.CreateVlanAsync(
                SelectedNetworkId,
                payload
            );
        }

        StatusMessage = $"VLAN {VlanId} restored successfully.";

        LoadBackupFolders();
        LoadVlanFiles(SelectedBackup!);
        LoadVlans(SelectedBackup!, SelectedFile!);
        await LoadNetworksAsync();

        return Page();
    }
    public async Task<IActionResult> OnPostPreview()
    {
        LoadBackupFolders();

        if (!string.IsNullOrWhiteSpace(SelectedBackup))
            LoadVlanFiles(SelectedBackup);

        if (!string.IsNullOrWhiteSpace(SelectedBackup) &&
            !string.IsNullOrWhiteSpace(SelectedFile))
            LoadVlans(SelectedBackup, SelectedFile);

        await LoadNetworksAsync();

        if (string.IsNullOrWhiteSpace(SelectedNetworkId))
        {
            PreviewMessage = "Select a target network first.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(VlanId))
        {
            PreviewMessage = "No VLAN selected.";
            return Page();
        }

        var existingVlansJson = await _merakiApiClient.GetVlansRawAsync(SelectedNetworkId);

        var existingVlans = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(existingVlansJson);

        var vlanExists = existingVlans?.Any(v =>
            v.ContainsKey("id") &&
            v["id"]?.ToString() == VlanId
        ) == true;

        if (vlanExists)
        {
            PreviewMessage = $"Pre-flight: VLAN {VlanId} already exists on the target network and will be UPDATED.";
        }
        else
        {
            PreviewMessage = $"Pre-flight: VLAN {VlanId} does not exist on the target network and will be CREATED.";
        }

        return Page();
    }
}