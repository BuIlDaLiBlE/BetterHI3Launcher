using System;

#nullable enable
namespace BetterHI3Launcher.Config;

public class FileMetadata
{
    public string?        DownloadUrl  { get; set; }
    public DateTimeOffset ModifiedDate { get; set; }
    public long           FileSize     { get; set; }
    public string?        Title        { get; set; }
}