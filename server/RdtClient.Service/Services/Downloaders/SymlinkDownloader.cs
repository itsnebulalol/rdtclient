using Serilog;

namespace RdtClient.Service.Services.Downloaders;

public class SymlinkDownloader : IDownloader
{
    public event EventHandler<DownloadCompleteEventArgs>? DownloadComplete;
    public event EventHandler<DownloadProgressEventArgs>? DownloadProgress;

    private readonly String _filePath;
    private readonly String _uri;

    private readonly CancellationTokenSource _cancellationToken = new();

    private readonly ILogger _logger;

    public SymlinkDownloader(String uri, String filePath)
    {
        _logger = Log.ForContext<SymlinkDownloader>();
        _uri = uri;
        _filePath = filePath;
    }

    public async Task<string?> Download()
    {
        _logger.Debug($"Starting symlink resolving of {_uri}, writing to path: {_filePath}");
        var filePath = new DirectoryInfo(_filePath);
        _logger.Debug($"Writing to path: ${filePath}");
        var fileName = filePath.Name;
        var fileExtension = filePath.Extension;
        var fileDirectory = Path.GetFileName(Path.GetDirectoryName(filePath.FullName));

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var fileDirectoryWithoutExtension = Path.GetFileNameWithoutExtension(fileDirectory);

        string[] folders = { fileNameWithoutExtension, fileDirectoryWithoutExtension, fileName, fileDirectory };


        List<string> unWantedExtensions = new()
        {
            "zip", "rar", "tar"
        };

        if (unWantedExtensions.Any(unwanted => "." + fileExtension == unwanted))
        {
            DownloadComplete?.Invoke(this, new DownloadCompleteEventArgs
            {
                Error = $"Cant handle compressed files with symlink downloader"
            });
            return null;
        }

        DownloadProgress?.Invoke(this, new DownloadProgressEventArgs
        {
            BytesDone = 0,
            BytesTotal = 0,
            Speed = 0
        });

        FileInfo? file = null;
        var tries = 0;
        while (file == null && tries <= 10)
        {
            _logger.Debug($"Searching {Settings.Get.DownloadClient.RcloneMountPath} for {fileName} ({tries})...");
            file = TryGetFileFromFolders(folders, fileName);
            await Task.Delay(1000);
            tries++;
        }

        if (file != null)
        {

            var result = TryCreateSymbolicLink(file.FullName, filePath.FullName);

            if (result)
            {
                DownloadComplete?.Invoke(this, new DownloadCompleteEventArgs());

                return file.FullName;
            }
        }

        DownloadComplete?.Invoke(this, new DownloadCompleteEventArgs
        {
            Error = "Could not find file from rclone mount!"
        });

        return null;

    }

    public Task Cancel()
    {
        _cancellationToken.Cancel(false);

        return Task.CompletedTask;
    }

    public Task Pause()
    {
        return Task.CompletedTask;
    }

    public Task Resume()
    {
        return Task.CompletedTask;
    }

    private bool TryCreateSymbolicLink(string sourcePath, string symlinkPath)
    {
        try
        {
            File.CreateSymbolicLink(symlinkPath, sourcePath);
            if (File.Exists(symlinkPath))  // Double-check that the link was created
            {
                _logger.Information($"Created symbolic link from {sourcePath} to {symlinkPath}");
                return true;
            }
            else
            {
                _logger.Error($"Failed to create symbolic link from {sourcePath} to {symlinkPath}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error creating symbolic link from {sourcePath} to {symlinkPath}: {ex.Message}");
            return false;
        }
    }
    private static FileInfo? TryGetFileFromFolders(string[] Folders, string File)
    {
        var dirInfo = new DirectoryInfo(Settings.Get.DownloadClient.RcloneMountPath);
        return dirInfo.EnumerateDirectories()
            .FirstOrDefault(dir => Folders.Contains(dir.Name), null)?
                .EnumerateFiles()
                .FirstOrDefault(x => x.Name == File, null);
    }
}
