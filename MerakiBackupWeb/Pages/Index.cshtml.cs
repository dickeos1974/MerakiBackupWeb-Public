using MerakiBackupWeb.Models;
using MerakiBackupWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace MerakiBackupWeb.Pages;

public class IndexModel : PageModel
{
    private readonly MerakiBackupRunner _backupRunner;
    private readonly BackupProgress _progress;

    public string StatusMessage { get; set; } = "";

    public IndexModel(
        MerakiBackupRunner backupRunner,
        BackupProgress progress,
        IConfiguration configuration)
    {
        _backupRunner = backupRunner;
        _progress = progress;
        _configuration = configuration;
    }

    public void OnGet()
    {
        StatusMessage = "Click 'Run Backup' to start.";
    }

    public IActionResult OnGetProgress()
    {
        return new JsonResult(new { status = _progress.Status });
    }

    public async Task OnPostAsync()
    {
        var result = await _backupRunner.RunBackupAsync();

        StatusMessage = result.Message;
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Replace(" ", "_");
    }
    private readonly IConfiguration _configuration;
    public async Task<IActionResult> OnGetRunScheduledBackup(string key)
    {
        var expectedKey = _configuration["Scheduler:Key"];

        if (key != expectedKey)
            return Unauthorized();

        var result = await _backupRunner.RunBackupAsync();

        return new JsonResult(new
        {
            success = result.Success,
            message = result.Message
        });
    }
}