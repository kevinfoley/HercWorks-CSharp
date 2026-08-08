namespace HercWorks.Core.Data.Struct;

/// <summary>
/// Hardcoded LUT for sake of development speed.
/// TODO (carried over from Java): eventually abstract this to a configurable list, or check for
/// WEAPONS.DAT in the shell (or /SIMVOL/WEAPONS.DAT).
/// Ported from org.hercworks.core.data.struct.WeaponLUT.
/// </summary>
public sealed class WeaponLUT {
	public static readonly WeaponLUT None = new(0, "NONE", 0);
	public static readonly WeaponLUT Atc20 = new(1, "ATC20", 1);
	public static readonly WeaponLUT Atc35 = new(2, "ATC35", 2);
	public static readonly WeaponLUT Atc50 = new(3, "ATC50", 3);
	public static readonly WeaponLUT Atc75 = new(4, "ATC75", 4);
	public static readonly WeaponLUT Atc100 = new(5, "ATC100", 5);
	public static readonly WeaponLUT Elfw = new(6, "ELFW", 6);
	public static readonly WeaponLUT Empc = new(7, "EMPC", 7);
	public static readonly WeaponLUT Las100 = new(8, "L100", 8);
	public static readonly WeaponLUT Las200 = new(9, "L200", 9);
	public static readonly WeaponLUT Las300 = new(10, "L300", 10);
	public static readonly WeaponLUT Las400 = new(11, "L400", 11);
	public static readonly WeaponLUT Las500 = new(12, "L500", 12);
	public static readonly WeaponLUT Msl6 = new(13, "MSL6", 13);
	public static readonly WeaponLUT Msl8 = new(14, "MSL8", 14);
	public static readonly WeaponLUT Msl10 = new(15, "MSL10", 15);
	public static readonly WeaponLUT Mslr = new(16, "MSLR", 16);
	public static readonly WeaponLUT Pbw = new(17, "PBW", 17);
	public static readonly WeaponLUT Ecm = new(18, "ECM", 18);
	public static readonly WeaponLUT Bemp = new(19, "BEMP", 0);
	public static readonly WeaponLUT Bpbw = new(20, "BPBW", 0);
	public static readonly WeaponLUT Bmsl = new(21, "BMSL", 0);
	public static readonly WeaponLUT Elf2 = new(22, "ELF2", 19);
	public static readonly WeaponLUT Emp2 = new(23, "EMP2", 20);
	public static readonly WeaponLUT Pbw2 = new(24, "PBW2", 21);
	public static readonly WeaponLUT Plas = new(25, "PLAS", 22);
	public static readonly WeaponLUT Laew = new(26, "LAEW", 0);
	public static readonly WeaponLUT Mine = new(27, "MINE", 0);
	public static readonly WeaponLUT Mfac = new(28, "MFAC", 0);
	public static readonly WeaponLUT Targ = new(29, "TARG", 26);
	public static readonly WeaponLUT Shld = new(30, "SHLD", 27);
	public static readonly WeaponLUT Turb = new(31, "TURB", 28);
	public static readonly WeaponLUT Enrg = new(32, "ENRG", 29);

	private static readonly IReadOnlyList<WeaponLUT> All = new[]
	{
		None, Atc20, Atc35, Atc50, Atc75, Atc100, Elfw, Empc, Las100, Las200, Las300, Las400, Las500,
		Msl6, Msl8, Msl10, Mslr, Pbw, Ecm, Bemp, Bpbw, Bmsl, Elf2, Emp2, Pbw2, Plas, Laew, Mine, Mfac,
		Targ, Shld, Turb, Enrg
	};

	private static readonly Dictionary<int, WeaponLUT> ById = All.ToDictionary(w => w.Id);

	public int Id { get; }
	public string Name { get; }
	public int SecondId { get; }

	private WeaponLUT(int id, string name, int secondId) {
		Id = id;
		Name = name;
		SecondId = secondId;
	}

	public static WeaponLUT? GetById(int id) => ById.GetValueOrDefault(id);

	/// <summary>Equivalent of Java's enum .values().</summary>
	public static IReadOnlyList<WeaponLUT> Values() => All;

	public static WeaponLUT? GetByName(string name) => All.FirstOrDefault(w => w.Name == name);

	public override string ToString() => Name;
}
