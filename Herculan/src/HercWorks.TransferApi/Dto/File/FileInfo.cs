using System.Text.Json.Serialization;

namespace HercWorks.TransferApi.Dto.File;

/// <summary>Ported from org.hercworks.transfer.dto.file.FileInfo.</summary>
public class FileInfo
{
    [JsonPropertyName("file")]
    [JsonPropertyOrder(0)]
    public string? FileName { get; set; }

    [JsonPropertyName("ext")]
    [JsonPropertyOrder(1)]
    public string? FileExt { get; set; }

    [JsonPropertyName("dir")]
    [JsonPropertyOrder(2)]
    public string? Dir { get; set; }
}
