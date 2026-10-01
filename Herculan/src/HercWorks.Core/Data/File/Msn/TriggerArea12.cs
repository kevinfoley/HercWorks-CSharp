namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #9 (12 bytes/record) — a trigger area, <c>script.dat</c> block 4: a ground-plane box between
/// two points or a circle about one. A mission action (<see cref="MissionAction82.AreaRefs"/>)
/// activates when its subject stands in one. See docs/formats/msn-mission-file.md, "Row #9 field
/// decode", and docs/simulation/mission-deployment.md#the-areas--block-4-resolved-by-triggerarea_resolve-00423358.
/// </summary>
public class TriggerArea12 : MapObject {
	/// <summary>0x02 — condition ref.</summary>
	public short ConditionRef { get; set; }

	/// <summary>0x04 — always -1 in retail; the load does not read it.</summary>
	public short Unk04 { get; set; }

	/// <summary>0x06 — 0 a box between <see cref="PointRef"/> and a second point, anything else a circle about <see cref="PointRef"/>.</summary>
	public short Shape { get; set; }

	/// <summary>0x08 — ref into row #6 (<see cref="MapPoint22"/>): the box's first corner or the circle's centre.</summary>
	public short PointRef { get; set; }

	/// <summary>0x0A — for a box, a ref into row #6, the opposite corner; for a circle, the radius in tens of world units (DBSIM multiplies it by 10).</summary>
	public short SecondPointOrRadius { get; set; }
}
