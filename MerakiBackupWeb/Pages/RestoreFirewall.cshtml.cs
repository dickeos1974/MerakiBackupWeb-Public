using MerakiBackupWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace MerakiBackupWeb.Pages;

public class RestoreFirewallModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly MerakiApiClient _merakiApiClient;

    public string StatusMessage { get; set; } = "";
    public string PreviewMessage { get; set; } = "";

    public List<string> BackupFolders { get; set; } = new();
    public List<string> FirewallFiles { get; set; } = new();
    public List<Dictionary<string, object>> Rules { get; set; } = new();
    public List<Dictionary<string, object>> Networks { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SelectedBackup { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedFile { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedNetworkId { get; set; }

    [BindProperty]
    public bool ConfirmRestore { get; set; }

    public RestoreFirewallModel(
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
            LoadFirewallFiles(SelectedBackup);

        if (!string.IsNullOrWhiteSpace(SelectedBackup) &&
            !string.IsNullOrWhiteSpace(SelectedFile))
            LoadFirewallRules(SelectedBackup, SelectedFile);

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

    private void LoadFirewallFiles(string backupFolder)
    {
        var folderPath = Path.Combine(GetBackupRoot(), backupFolder);

        if (!Directory.Exists(folderPath))
            return;

        FirewallFiles = Directory
            .GetFiles(folderPath, "firewall_*.json")
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x)
            .ToList()!;
    }

    private void LoadFirewallRules(string backupFolder, string fileName)
    {
        var filePath = Path.Combine(GetBackupRoot(), backupFolder, fileName);

        if (!System.IO.File.Exists(filePath))
            return;

        var json = System.IO.File.ReadAllText(filePath);

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.TryGetProperty("rules", out var rulesElement))
        {
            var rules = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(rulesElement.GetRawText());

            if (rules != null)
                Rules = rules;
        }
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

    public async Task<IActionResult> OnPostRestore()
    {
        await Reload();

        if (string.IsNullOrWhiteSpace(SelectedNetworkId))
        {
            StatusMessage = "Please select a target network.";
            return Page();
        }

        if (!ConfirmRestore)
        {
            StatusMessage = "Please confirm restore before proceeding.";
            return Page();
        }

        var orgId = _configuration["Meraki:OrgId"];

        var hasTag = await _merakiApiClient.NetworkHasTagAsync(
            orgId!,
            SelectedNetworkId,
            "merakiRestore"
        );

        if (!hasTag)
        {
            StatusMessage = "Restore blocked: target network does not have 'merakiRestore' tag.";
            return Page();
        }

        var filePath = Path.Combine(GetBackupRoot(), SelectedBackup!, SelectedFile!);
        var json = System.IO.File.ReadAllText(filePath);

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("rules", out var rulesElement))
        {
            StatusMessage = "Invalid firewall backup file.";
            return Page();
        }

        var payload = new
        {
            rules = JsonSerializer.Deserialize<object>(rulesElement.GetRawText())
        };

        await _merakiApiClient.UpdateFirewallRulesAsync(
            SelectedNetworkId,
            payload
        );

        StatusMessage = $"Firewall rules restored successfully ({Rules.Count} rules).";

        await Reload();

        return Page();
    }
    private async Task Reload()
    {
        LoadBackupFolders();

        if (!string.IsNullOrWhiteSpace(SelectedBackup))
            LoadFirewallFiles(SelectedBackup);

        if (!string.IsNullOrWhiteSpace(SelectedBackup) &&
            !string.IsNullOrWhiteSpace(SelectedFile))
            LoadFirewallRules(SelectedBackup, SelectedFile);

        await LoadNetworksAsync();
    }
}