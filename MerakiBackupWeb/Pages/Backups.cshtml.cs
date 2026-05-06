using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace MerakiBackupWeb.Pages;

public class BackupsModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public List<BackupFolder> BackupFolders { get; set; } = new();

    public BackupsModel(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public IActionResult OnPostOpen(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start("explorer.exe", path);
        }

        return RedirectToPage();
    }

    public void OnGet()
    {
        var backupPath = _configuration["Backup:BackupPath"] ?? "Backups";

        var fullBackupPath = Path.Combine(
            _environment.ContentRootPath,
            backupPath
        );

        if (!Directory.Exists(fullBackupPath))
            return;

        BackupFolders = Directory
            .GetDirectories(fullBackupPath)
            .Select(folder => new BackupFolder
            {
                Name = Path.GetFileName(folder),
                FullPath = folder,
                FileCount = Directory.GetFiles(folder).Length,
                Created = Directory.GetCreationTime(folder)
            })
            .OrderByDescending(x => x.Created)
            .ToList();
    }
    public IActionResult OnPostDownloadZip(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return RedirectToPage();

        var folderName = Path.GetFileName(path);
        var zipFileName = $"{folderName}.zip";

        using var memoryStream = new MemoryStream();

        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var file in Directory.GetFiles(path))
            {
                var entryName = Path.GetFileName(file);
                var entry = archive.CreateEntry(entryName);

                using var entryStream = entry.Open();
                using var fileStream = System.IO.File.OpenRead(file);

                fileStream.CopyTo(entryStream);
            }
        }

        memoryStream.Position = 0;

        return File(
            memoryStream.ToArray(),
            "application/zip",
            zipFileName
        );
    }
}

public class BackupFolder
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public int FileCount { get; set; }
    public DateTime Created { get; set; }
}