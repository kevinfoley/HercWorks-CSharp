using System.Text.Json.Serialization;

namespace HercWorks.TransferApi.Dto.Struct.Dbsim;

/// <summary>Ported from org.hercworks.transfer.dto.struct.dbsim.HercDmgPieceDTO.</summary>
public class HercDmgPieceDTO
{
    [JsonPropertyName("armor")]
    [JsonPropertyOrder(0)]
    public int Armor { get; set; }

    [JsonPropertyName("debris_flag")]
    [JsonPropertyOrder(1)]
    public int DebrisFlags { get; set; }

    [JsonPropertyName("bone_id")]
    [JsonPropertyOrder(2)]
    public int BoneId { get; set; }

    [JsonPropertyName("unk1_val")]
    [JsonPropertyOrder(3)]
    public int Unk1_val { get; set; }

    [JsonPropertyName("linked_internals")]
    [JsonPropertyOrder(4)]
    public Dictionary<string, float> Internals { get; set; } = new();
}
