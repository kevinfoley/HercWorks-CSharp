using HercWorks.Core.Data.File.Sav;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Core.Data.Struct.Vshell.Sav;
using HercWorks.Vol;
using System.Text;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>Ported from org.hercworks.core.io.transform.common.PlayerSaveTransform.</summary>
public class PlayerSaveTransform : ThreeSpaceByteTransformer {
	private int _dbgBuffer;

	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var save = new PlayerSave {
			Ext = FileType.Sav,
			Dir = FileType.Sav
		};

		// INVENTORY SEGMENT - 33 entries, matches total weapons in game, ERROR/cut weapon ids
		// ARE included here, but zeroed out.
		var inventory = new Inventory {
			Items = new Inventory.InventoryItem[WeaponLUT.Values().Count]
		};
		for (int i = 0; i < WeaponLUT.Values().Count; i++) {
			byte flag = IndexByte();
			short quant = IndexShortLE();
			var entry = inventory.NewEntry();
			entry.Id = WeaponLUT.GetById(i);
			entry.UnlockFlag = flag;
			entry.Quantity = quant;

			var items = new ShellWeaponEntry[quant];
			for (int q = 0; q < quant; q++) {
				var weapon = new ShellWeaponEntry {
					Id = WeaponLUT.GetById(IndexShortLE()),
					NameId = IndexShortLE(),
					HealthArmor = IndexShortLE(),
					HealthInteral = IndexShortLE(),
					MissileType = MissileType.GetById(IndexShortLE())
				};
				items[q] = weapon;
			}
			entry.Data = items;

			inventory.Items[i] = entry;
		}
		save.Inventory = inventory;

		// Workshop state
		save.WorkshopSpace = IndexShortLE();
		for (int w = 0; w < save.WorkshopSlots.Length; w++) {
			IndexShortLE(); // why would these ever be out of order?
			save.WorkshopSlots[w] = WeaponLUT.GetById(IndexShortLE())!;
		}

		// Campaign flags, series of INT16's
		for (int f = 0; f < save.Unk4_stateFlags.Length; f++) {
			save.Unk4_stateFlags[f] = IndexShortLE();
		}

		// Squadmate segment
		var squad = new PilotEntry[36]; // 36 squadmates
		for (int s = 0; s < squad.Length; s++) {
			squad[s] = IndexSquadmate();
		}
		save.Squadmates = squad;

		// Unknown post-pilot, pre-player 9-short range.
		for (int r = 0; r < save.UnkRange_prePlayer.Length; r++) {
			save.UnkRange_prePlayer[r] = IndexShortLE();
		}

		// Pilot segment
		save.PlayerPilot = IndexPlayerPilot();

		// Herc bay data
		short baySlots = IndexShortLE();
		for (int b = 0; b < baySlots; b++) {
			short bayId = IndexShortLE();
			save.HercBay[bayId] = IndexHercEntry();
		}

		// Herc unlock flags — 9 shorts (18 bytes)
		int l = 0;
		while (l < HercLUT.Mongoose.Id) {
			short val = IndexShortLE();
			save.UnlockedHercs[HercLUT.GetById((short)l)!] = val;
			l += 1;
		}

		// Total available salvage
		save.SalvageTotal = IndexIntLE();

		// Unknown tail bytes
		using var fragmentFlags = new MemoryStream();
		while (Index < GetBytes().Length) {
			byte b = IndexByte();
			fragmentFlags.WriteByte(b);
		}
		save.UnknownSaveValues = fragmentFlags.ToArray();

		return save;
	}

	private PilotEntry IndexSquadmate() {
		var entry = new PilotEntry {
			SquadmateId = IndexShortLE()
		};

		short nameLen = IndexShortLE();
		byte[] name = IndexSegment(nameLen);

		entry.Name = BytesToLatin1String(name).Substring(0, nameLen - 1);

		entry.BayId = IndexShortLE();
		entry.Active = IndexByte();
		entry.Rank = PilotRank.GetById(IndexShortLE());
		entry.CrewRowNum = IndexShortLE();
		entry.Unk2Uint16 = IndexShortLE();
		entry.ProbablyHealth = IndexShortLE();
		entry.KillsHercs = IndexShortLE();
		entry.KillsFlyers = IndexShortLE();
		entry.KillsBuilding = IndexShortLE();
		entry.TotalKillHerc = IndexShortLE();
		entry.TotalKillFlyer = IndexShortLE();
		entry.TotalKillBldng = IndexShortLE();
		entry.MissionCount = IndexShortLE();
		entry.Unk5Uint16 = IndexShortLE();

		return entry;
	}

	/// <summary>Player data drops the last shorts vs an AI squadmate's data, not sure why ATM.</summary>
	private PilotEntry IndexPlayerPilot() {
		var entry = new PilotEntry();

		short nameLen = IndexShortLE();
		byte[] name = IndexSegment(nameLen);

		entry.Name = BytesToLatin1String(name).Substring(0, nameLen - 1);
		entry.BayId = IndexShortLE();
		entry.Active = IndexByte();
		entry.Rank = PilotRank.GetById(IndexShortLE());
		entry.CrewRowNum = IndexShortLE();
		entry.Unk2Uint16 = IndexShortLE();
		entry.ProbablyHealth = IndexShortLE();
		entry.KillsHercs = IndexShortLE();
		entry.KillsFlyers = IndexShortLE();
		entry.KillsBuilding = IndexShortLE();
		entry.TotalKillHerc = IndexShortLE();
		entry.TotalKillFlyer = IndexShortLE();
		entry.TotalKillBldng = IndexShortLE();
		entry.MissionCount = IndexShortLE();

		return entry;
	}

	private HercBayEntry IndexHercEntry() {
		var herc = new HercBayEntry {
			Id = HercLUT.GetById(IndexShortLE()),
			NameId = IndexShortLE(),
			HealthExternals = new Dictionary<HercExternals, ShellHercPart>()
		};

		foreach (var e in HercExternals.Values()) {
			herc.HealthExternals[e] = new ShellHercPart(e.Id, e.Label, IndexShortLE());
		}

		// TODO (carried over from Java): struct here caps internals to just bipedal hercs.
		herc.HealthInternals = new Dictionary<HercInternals, ShellHercPart>();
		foreach (var internalPart in HercInternals.Values()) {
			if (internalPart.Id < HercInternals.ServosLegLeftRear.Id) {
				herc.HealthInternals[internalPart] = new ShellHercPart(internalPart.Id, internalPart.Label, IndexShortLE());
			}
		}

		for (int h = 0; h < herc.HealthHardpoints.Length; h++) {
			herc.HealthHardpoints[h] = new ShellHercPart((short)h, "hardpoint_" + h, IndexShortLE());
		}

		herc.BuildPercent = IndexShortLE();
		herc.BuildStepNum = IndexShortLE();

		herc.HardpointMax = IndexShortLE();
		herc.ActiveSockets = IndexShortLE();
		for (int h = 0; h < herc.ActiveSockets; h++) {
			short socketId = IndexShortLE();
			var weapon = new ShellWeaponEntry {
				Id = WeaponLUT.GetById(IndexShortLE()),
				NameId = IndexShortLE(),
				HealthArmor = IndexShortLE(),
				HealthInteral = IndexShortLE(),
				MissileType = MissileType.GetById(IndexShortLE())
			};
			herc.Weapons[socketId] = weapon;
		}

		return herc;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		var save = (PlayerSave)source!;

		using var outStream = new MemoryStream();

		// INVENTORY SEGMENT
		foreach (var item in save.Inventory!.Items!) {
			outStream.WriteByte((byte)item.UnlockFlag);
			_dbgBuffer += 1;
			WriteAndCount(outStream, WriteShortLE(item.Quantity));

			foreach (var entry in item.Data!) {
				WriteAndCount(outStream, WriteShortLE((short)entry.Id!.Id));
				WriteAndCount(outStream, WriteShortLE(entry.NameId));
				WriteAndCount(outStream, WriteShortLE(entry.HealthArmor));
				WriteAndCount(outStream, WriteShortLE(entry.HealthInteral));
				WriteAndCount(outStream, WriteShortLE((short)entry.MissileType!.Id));
			}
		}

		// WORKSHOP SLOTS
		WriteAndCount(outStream, WriteShortLE(save.WorkshopSpace));
		for (int w = 0; w < save.WorkshopSlots.Length; w++) {
			WriteAndCount(outStream, WriteShortLE((short)w));
			WriteAndCount(outStream, WriteShortLE((short)save.WorkshopSlots[w].Id));
		}

		// CAMPAIGN FLAGS
		foreach (var f in save.Unk4_stateFlags) {
			WriteAndCount(outStream, WriteShortLE(f));
		}

		// PILOT DATA
		foreach (var pilot in save.Squadmates!) {
			WritePilotData(pilot, outStream, false);
		}

		// UNKNOWN PILOT DATA
		foreach (var unk in save.UnkRange_prePlayer) {
			WriteAndCount(outStream, WriteShortLE(unk));
		}

		// PLAYER PILOT
		WritePilotData(save.PlayerPilot!, outStream, true);

		// HERC DATA
		outStream.Write(WriteShortLE((short)save.HercBay.Count), 0, 2);
		for (short h = 0; h < save.HercBay.Count; h++) {
			WriteHercEntry(h, save.HercBay[h], outStream);
		}

		// HERC UNLOCKS — mirrors the read path exactly: same id range (0 until HercLUT.Mongoose.Id),
		// same source (save.UnlockedHercs), instead of writing a hardcoded pattern over an
		// unrelated id range. See KNOWN_ISSUES.md history for the bug this replaces.
		for (short l = 0; l < HercLUT.Mongoose.Id; l++) {
			var herc = HercLUT.GetById(l)!;
			short val = save.UnlockedHercs.TryGetValue(herc, out var unlockVal) ? unlockVal : (short)0;
			WriteAndCount(outStream, WriteShortLE(val));
		}

		// SALVAGE
		WriteAndCount(outStream, WriteIntLE(save.SalvageTotal));

		// UNKNOWN TAIL SEGMENT
		foreach (var b in save.UnknownSaveValues!) {
			outStream.WriteByte(b);
			_dbgBuffer += 1;
		}

		return outStream.ToArray();
	}

	private void WritePilotData(PilotEntry pilot, MemoryStream outArr, bool isPlayer) {
		if (!isPlayer) {
			WriteAndCount(outArr, WriteShortLE(pilot.SquadmateId));
		}

		var nameBytes = Encoding.UTF8.GetBytes(pilot.Name ?? string.Empty);
		var arr = new byte[nameBytes.Length + 1];
		Array.Copy(nameBytes, arr, nameBytes.Length);
		arr[^1] = 0x00;

		WriteAndCount(outArr, WriteShortLE((short)arr.Length));
		outArr.Write(arr, 0, arr.Length);
		_dbgBuffer += arr.Length;

		WriteAndCount(outArr, WriteShortLE(pilot.BayId));
		outArr.WriteByte(pilot.Active);
		_dbgBuffer += 1;
		WriteAndCount(outArr, WriteShortLE(pilot.Rank!.Id));
		WriteAndCount(outArr, WriteShortLE(pilot.CrewRowNum));
		WriteAndCount(outArr, WriteShortLE(pilot.Unk2Uint16));
		WriteAndCount(outArr, WriteShortLE(pilot.ProbablyHealth));
		WriteAndCount(outArr, WriteShortLE(pilot.KillsHercs));
		WriteAndCount(outArr, WriteShortLE(pilot.KillsFlyers));
		WriteAndCount(outArr, WriteShortLE(pilot.KillsBuilding));
		WriteAndCount(outArr, WriteShortLE(pilot.TotalKillHerc));
		WriteAndCount(outArr, WriteShortLE(pilot.TotalKillFlyer));
		WriteAndCount(outArr, WriteShortLE(pilot.TotalKillBldng));
		WriteAndCount(outArr, WriteShortLE(pilot.MissionCount));

		if (!isPlayer) {
			WriteAndCount(outArr, WriteShortLE(pilot.Unk5Uint16));
		}
	}

	private void WriteHercEntry(short bayId, HercBayEntry herc, MemoryStream outArr) {
		WriteAndCount(outArr, WriteShortLE(bayId));
		WriteAndCount(outArr, WriteShortLE(herc.Id!.Id));
		WriteAndCount(outArr, WriteShortLE(herc.NameId));

		foreach (var external in HercExternals.Values()) {
			WriteAndCount(outArr, WriteShortLE(herc.HealthExternals![external].Health));
		}

		foreach (var internalPart in HercInternals.Values()) {
			if (internalPart.Id < HercInternals.ServosLegLeftRear.Id) {
				WriteAndCount(outArr, WriteShortLE(herc.HealthInternals![internalPart].Health));
			}
		}

		foreach (var part in herc.HealthHardpoints) {
			if (part == null) {
				WriteAndCount(outArr, WriteShortLE(100));
			} else {
				WriteAndCount(outArr, WriteShortLE(part.Health));
			}
		}

		WriteAndCount(outArr, WriteShortLE(herc.BuildPercent));
		WriteAndCount(outArr, WriteShortLE(herc.BuildStepNum));

		WriteAndCount(outArr, WriteShortLE(herc.HardpointMax));
		WriteAndCount(outArr, WriteShortLE(herc.ActiveSockets));

		for (short w = 0; w < herc.ActiveSockets; w++) {
			var weapon = herc.Weapons[w];
			WriteAndCount(outArr, WriteShortLE(w));
			WriteAndCount(outArr, WriteShortLE((short)weapon.Id!.Id));
			WriteAndCount(outArr, WriteShortLE(weapon.NameId));
			WriteAndCount(outArr, WriteShortLE(weapon.HealthArmor));
			WriteAndCount(outArr, WriteShortLE(weapon.HealthInteral));
			WriteAndCount(outArr, WriteShortLE((short)weapon.MissileType!.Id));
		}
	}

	private void WriteAndCount(MemoryStream outArr, byte[] data) {
		outArr.Write(data, 0, data.Length);
		_dbgBuffer += data.Length;
	}

	/// <summary>Same zero-extend-per-byte string decode used by IndexString/NameFromListBytes.</summary>
	private static string BytesToLatin1String(byte[] data) {
		var chars = new char[data.Length];
		for (int i = 0; i < data.Length; i++) {
			chars[i] = (char)data[i];
		}
		return new string(chars);
	}
}
