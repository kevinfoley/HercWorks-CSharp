using System.Text.Json.Serialization;
using HercWorks.TransferApi.Dto.File.Shell;
using HercWorks.TransferApi.Dto.File.Sim;
using HercWorks.TransferApi.Dto.File.Sim.Dts;

namespace HercWorks.TransferApi.Dto.File;

/// <summary>
/// Meta class for generics. Ported from org.hercworks.transfer.dto.file.TransferObject.
/// The Java original used Jackson's @JsonTypeInfo/@JsonSubTypes for polymorphic (de)serialization
/// keyed on a "classDef" property; System.Text.Json's [JsonPolymorphic]/[JsonDerivedType] is the
/// direct equivalent.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "classDef")]
[JsonDerivedType(typeof(ArmHercDTO), "ArmHerc")]
[JsonDerivedType(typeof(ArmWeapDTO), "ArmWeap")]
[JsonDerivedType(typeof(CareerMissionsDTO), "CareerMissions")]
[JsonDerivedType(typeof(HardpointOverlayDTO), "HardpointOverlayConfig")]
[JsonDerivedType(typeof(StartHercsDTO), "Hercs")]
[JsonDerivedType(typeof(HercInfDTO), "HercInf")]
[JsonDerivedType(typeof(InitHercDTO), "InitHerc")]
[JsonDerivedType(typeof(RepairHercDTO), "RprHerc")]
[JsonDerivedType(typeof(TrainingHercsDTO), "TrainingHercs")]
[JsonDerivedType(typeof(WeaponsDatDTO), "WeaponsDat")]
[JsonDerivedType(typeof(DebrisHercDTO), "DebrisHerc")]
[JsonDerivedType(typeof(BeamDatDTO), "BeamData")]
[JsonDerivedType(typeof(FlightModelDTO), "FlightModel")]
[JsonDerivedType(typeof(GunLayoutDTO), "GunLayout")]
[JsonDerivedType(typeof(HercDmgDTO), "HercSimDamage")]
[JsonDerivedType(typeof(HercSimDatDTO), "HercSimDat")]
[JsonDerivedType(typeof(MissileDatDTO), "MissileDatFile")]
[JsonDerivedType(typeof(PaperDollDTO), "PaperDollGraphic")]
[JsonDerivedType(typeof(ProjectileDataDTO), "ProjectileData")]
[JsonDerivedType(typeof(WpnPDGDTO), "WeaponPaperDiagram")]
[JsonDerivedType(typeof(DTSRootDTO), "DTSModel")]
public abstract class TransferObject
{
    [JsonPropertyName("fileName")]
    [JsonPropertyOrder(0)]
    public string? FileName { get; set; }

    // utilities for clean presentation
    public short FloatStringToFixedShort(string strFloat)
    {
        float f = float.Parse(strFloat);
        f *= 100f;
        return (short)f;
    }

    public string FixedShortToFloatString(short val)
    {
        float f = val;
        return (f / 100f).ToString();
    }
}
