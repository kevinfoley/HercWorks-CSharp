using System.Text.Json.Serialization;

namespace HercWorks.TransferApi.Dto.Struct.Dbsim;

/// <summary>Ported from org.hercworks.transfer.dto.struct.dbsim.GunLayoutEntryDTO.</summary>
public class GunLayoutEntryDTO
{
    [JsonPropertyName("bone_id")]
    [JsonPropertyOrder(1)]
    public int BoneId { get; set; }

    [JsonPropertyName("unk1_val")]
    [JsonPropertyOrder(2)]
    public int Unk1_val { get; set; }

    [JsonPropertyName("unk2_val")]
    [JsonPropertyOrder(3)]
    public int Unk2_val { get; set; }

    [JsonPropertyName("option_angle_dir")]
    [JsonPropertyOrder(4)]
    public int AngleDirOption { get; set; }

    [JsonPropertyName("fire_chain_val")]
    [JsonPropertyOrder(5)]
    public int FireChainNumber { get; set; }

    [JsonPropertyName("unk3_0or_Neg5000")]
    [JsonPropertyOrder(6)]
    public int Unk3_0or_Neg5000 { get; set; }

    [JsonPropertyName("unk4_0or_5000")]
    [JsonPropertyOrder(7)]
    public int Unk4_0or_5000 { get; set; }

    [JsonPropertyName("unk5_Neg8000")]
    [JsonPropertyOrder(8)]
    public int Unk5_Neg8000 { get; set; }

    [JsonPropertyName("unk6_16000")]
    [JsonPropertyOrder(9)]
    public int Unk6_16000 { get; set; }

    [JsonPropertyName("offset_vector")]
    [JsonPropertyOrder(10)]
    public string[] Offset { get; set; } = new string[3];

    [JsonPropertyName("unk7_val")]
    [JsonPropertyOrder(11)]
    public int Unk7_val { get; set; }

    [JsonPropertyName("hardpoint_id")]
    [JsonPropertyOrder(12)]
    public int HardpointId { get; set; }

    [JsonPropertyName("unk8_val")]
    [JsonPropertyOrder(13)]
    public int Unk8_val { get; set; }
}
