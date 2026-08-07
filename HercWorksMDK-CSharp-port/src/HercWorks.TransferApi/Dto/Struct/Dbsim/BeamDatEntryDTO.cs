using System.Text.Json.Serialization;

namespace HercWorks.TransferApi.Dto.Struct.Dbsim;

/// <summary>Ported from org.hercworks.transfer.dto.struct.dbsim.BeamDatEntryDTO.</summary>
public class BeamDatEntryDTO
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(0)]
    public int Id { get; set; }

    [JsonPropertyName("width")]
    [JsonPropertyOrder(1)]
    public int Width { get; set; }

    [JsonPropertyName("color_id")]
    [JsonPropertyOrder(2)]
    public int ColorId { get; set; }

    [JsonPropertyName("dbaFrameNum")]
    [JsonPropertyOrder(3)]
    public int DbaFrameNum { get; set; }
}
