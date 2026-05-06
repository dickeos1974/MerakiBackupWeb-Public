using Microsoft.AspNetCore.Hosting;

namespace MerakiBackupWeb.Services;

public class BackupService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public BackupService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public string CreateBackupRunFolder()
    {
        var backupPath = _configuration["Backup:BackupPath"] ?? "Backups";

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");

        var folderPath = Path.Combine(
            _environment.ContentRootPath,
            backupPath,
            timestamp
        );

        Directory.CreateDirectory(folderPath);

        return folderPath;
    }

    public async Task<string> SaveBackupAsync(string folderPath, string fileName, string content)
    {
        var filePath = Path.Combine(folderPath, fileName);

        await File.WriteAllTextAsync(filePath, content);

        return filePath;
    }
}