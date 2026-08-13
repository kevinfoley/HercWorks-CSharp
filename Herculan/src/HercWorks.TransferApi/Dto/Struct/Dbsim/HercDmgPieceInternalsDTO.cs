using System.Text.Json.Serialization;

namespace HercWorks.TransferApi.Dto.Struct.Dbsim;

/// <summary>Ported from org.hercworks.transfer.dto.struct.dbsim.HercDmgPieceInternalsDTO.</summary>
public class HercDmgPieceInternalsDTO
{
    [JsonPropertyName("crit_chance")]
    [JsonPropertyOrder(0)]
    public float CritChance { get; set; } = 0.0f;

    [JsonPropertyName("internal_id")]
    [JsonPropertyOrder(1)]
    public string? InternalsId { get; set; }
}
