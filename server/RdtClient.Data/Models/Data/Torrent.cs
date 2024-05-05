﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using RdtClient.Data.Enums;
using RdtClient.Data.Models.TorrentClient;

namespace RdtClient.Data.Models.Data;

public class Torrent
{
    [Key]
    public Guid TorrentId { get; set; }

    public String Hash { get; set; } = null!;

    public String? Category { get; set; }
        
    public TorrentDownloadAction DownloadAction { get; set; }
    public TorrentFinishedAction FinishedAction { get; set; }
    public TorrentHostDownloadAction HostDownloadAction { get; set; }
    public Int32 DownloadMinSize { get; set; }
    public String? DownloadManualFiles { get; set; }

    public DateTimeOffset Added { get; set; }
    public DateTimeOffset? FilesSelected { get; set; }
    public DateTimeOffset? Completed { get; set; }
    public DateTimeOffset? Retry { get; set; }

    public String? FileOrMagnet { get; set; }
    public Boolean IsFile { get; set; }

    public Int32? Priority { get; set; }
    public Int32 RetryCount { get; set; }
    public Int32 DownloadRetryAttempts { get; set; }
    public Int32 TorrentRetryAttempts { get; set; }
    public Int32 DeleteOnError { get; set; }
    public Int32 Lifetime { get; set; }

    public String? Error { get; set; }

    [InverseProperty("Torrent")]
    public IList<Download> Downloads { get; set; } = new List<Download>();

    public String? RdId { get; set; }
    public String? RdName { get; set; }
    public Int64? RdSize { get; set; }
    public String? RdHost { get; set; }
    public Int64? RdSplit { get; set; }
    public Int64? RdProgress { get; set; }
    public TorrentStatus? RdStatus { get; set; }
    public String? RdStatusRaw { get; set; }
    public DateTimeOffset? RdAdded { get; set; }
    public DateTimeOffset? RdEnded { get; set; }
    public Int64? RdSpeed { get; set; }
    public Int64? RdSeeders { get; set; }
    public String? RdFiles { get; set; }

    [NotMapped]
    public IList<TorrentClientFile> Files
    {
        get
        {
            if (String.IsNullOrWhiteSpace(RdFiles))
            {
                return new List<TorrentClientFile>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<TorrentClientFile>>(RdFiles) ?? new List<TorrentClientFile>();
            }
            catch
            {
                return new List<TorrentClientFile>();
            }
        }
    }

    [NotMapped]
    public IList<String> ManualFiles
    {
        get
        {
            if (String.IsNullOrWhiteSpace(DownloadManualFiles))
            {
                return new List<String>();
            }

            return DownloadManualFiles.Split(",");
        }
    }
}