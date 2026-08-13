namespace HercWorks.Core.Data.Struct.Vshell.Hercs;

/// <summary>
/// As observed in PlayerSave files, and possible .MEC files. The values stored are not
/// sequential — example: left and right torsos list their front-armor value first, then rear
/// armor (Left Torso front, Right Torso front, Left Torso rear, Right Torso rear). Likewise,
/// internal components only have a single HP value for the entire component, while legs EACH
/// have 3 values.
/// Ported from org.hercworks.core.data.struct.vshell.hercs.ShellHercPart.
/// </summary>
public class ShellHercPart {
	public short Id { get; set; }
	public string? Label { get; set; }
	public short Health { get; set; }

	public ShellHercPart() { }

	public ShellHercPart(short id, string label) {
		Id = id;
		Label = label;
	}

	public ShellHercPart(short id, string label, short health) {
		Id = id;
		Label = label;
		Health = health;
	}

	public override string ToString() {
		return $"ShellHercPart [id={Id}, label={Label}, health={Health}]";
	}
}
