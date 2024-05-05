﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RdtClient.Data.Enums;
using RdtClient.Data.Models.Data;
using RdtClient.Service.Services;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace RdtClient.Service.BackgroundServices;

public class WatchFolderChecker : BackgroundService
{
    private readonly ILogger<WatchFolderChecker> _logger;
    private readonly IServiceProvider _serviceProvider;

    private DateTime _prevCheck = DateTime.MinValue;

    public WatchFolderChecker(ILogger<WatchFolderChecker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!Startup.Ready)
        {
            await Task.Delay(1000, stoppingToken);
        }

        using var scope = _serviceProvider.CreateScope();
        var torrentService = scope.ServiceProvider.GetRequiredService<Torrents>();
            
        _logger.LogInformation("WatchFolderChecker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, stoppingToken);

                if (String.IsNullOrWhiteSpace(Settings.Get.Watch.Path))
                {
                    continue;
                }

                var processedStorePath = Path.Combine(Settings.Get.Watch.Path, "processed");
                var errorStorePath = Path.Combine(Settings.Get.Watch.Path, "error");

                if (!String.IsNullOrWhiteSpace(Settings.Get.Watch.ProcessedPath))
                {
                    processedStorePath = Settings.Get.Watch.ProcessedPath;
                }

                if (!String.IsNullOrWhiteSpace(Settings.Get.Watch.ErrorPath))
                {
                    errorStorePath = Settings.Get.Watch.ErrorPath;
                }

                var nextCheck = _prevCheck.AddSeconds(Settings.Get.Watch.Interval);

                if (DateTime.UtcNow < nextCheck)
                {
                    continue;
                }

                _prevCheck = DateTime.UtcNow;

                var torrentFiles = Directory.GetFiles(Settings.Get.Watch.Path, "*.*", SearchOption.TopDirectoryOnly);

                foreach (var torrentFile in torrentFiles)
                {
                    var fileInfo = new FileInfo(torrentFile);

                    if (fileInfo.Extension != ".magnet" && fileInfo.Extension != ".torrent")
                    {
                        continue;
                    }

                    if (IsFileLocked(fileInfo))
                    {
                        continue;
                    }

                    try
                    {
                        _logger.Log(LogLevel.Debug, "Processing {torrentFile}", torrentFile);

                        var torrent = new Torrent
                        {
                            Category = Settings.Get.Watch.Default.Category,
                            HostDownloadAction = Settings.Get.Watch.Default.HostDownloadAction,
                            DownloadAction = Settings.Get.Watch.Default.OnlyDownloadAvailableFiles
                                ? TorrentDownloadAction.DownloadAvailableFiles
                                : TorrentDownloadAction.DownloadAll,
                            FinishedAction = Settings.Get.Watch.Default.FinishedAction,
                            DownloadMinSize = Settings.Get.Watch.Default.MinFileSize,
                            TorrentRetryAttempts = Settings.Get.Watch.Default.TorrentRetryAttempts,
                            DownloadRetryAttempts = Settings.Get.Watch.Default.DownloadRetryAttempts,
                            DeleteOnError = Settings.Get.Watch.Default.DeleteOnError,
                            Lifetime = Settings.Get.Watch.Default.TorrentLifetime,
                            Priority = Settings.Get.Watch.Default.Priority > 0 ? Settings.Get.Watch.Default.Priority : null
                        };

                        if (fileInfo.Extension == ".torrent")
                        {
                            var torrentFileContents = await File.ReadAllBytesAsync(torrentFile, stoppingToken);
                            await torrentService.UploadFile(torrentFileContents, torrent);
                        }
                        else if (fileInfo.Extension == ".magnet")
                        {
                            var magnetLink = await File.ReadAllTextAsync(torrentFile, stoppingToken);
                            await torrentService.UploadMagnet(magnetLink, torrent);
                        }

                        var processedPath = Path.Combine(processedStorePath, fileInfo.Name);

                        if (!Directory.Exists(processedStorePath))
                        {
                            Directory.CreateDirectory(processedStorePath);
                        }

                        File.Move(torrentFile, processedPath);

                        _logger.Log(LogLevel.Debug, "Moved {torrentFile} to {processedPath}", torrentFile, processedPath);
                    }
                    catch
                    {
                        if (!Directory.Exists(errorStorePath))
                        {
                            Directory.CreateDirectory(errorStorePath);
                        }

                        var processedPath = Path.Combine(errorStorePath, fileInfo.Name);
                        File.Move(torrentFile, processedPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error occurred in ProviderUpdater: {ex.Message}");
            }
        }
    }

    private static Boolean IsFileLocked(FileInfo file)
    {
        try
        {
            using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            stream.Close();
        }
        catch (IOException e) when ((e.HResult & 0x0000FFFF) == 32)
        {
            return true;
        }
        return false;
    }
}