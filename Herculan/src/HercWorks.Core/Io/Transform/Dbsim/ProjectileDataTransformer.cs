using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.Struct;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Ported from org.hercworks.core.io.transform.dbsim.ProjectileDataTransformer.</summary>
public class ProjectileDataTransformer : ByteTransformer<ProjectileData> {
	public override ProjectileData? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var data = new ProjectileData();

		data.Total = IndexShortLE();
		data.Data = new ProjectileData.Projectile[data.Total];

		for (int p = 0; p < data.Total; p++) {
			var proj = data.NewProjectile();

			proj.Type = ProjectileType.ForId(IndexShortLE());
			proj.MissileId = IndexShortLE();
			proj.DamageShield = IndexShortLE();
			proj.DamageArmor = IndexShortLE();
			proj.SplashFactor = IndexShortLE();
			proj.Speed = IndexShortLE();

			proj.ImpactFXShield[0] = IndexShortLE();
			proj.ImpactFXShield[1] = IndexShortLE();
			proj.ImpactFXShield[2] = IndexShortLE();
			proj.ImpactFXShield[3] = IndexShortLE();

			// this order is fairly wonky and probably requires more research.
			proj.ImpactFXGround[0] = IndexShortLE();
			proj.ImpactFXGround[1] = IndexShortLE();
			proj.ImpactFXGround[2] = IndexShortLE();
			proj.ImpactFXGround[3] = IndexShortLE();

			proj.ImpactFXArmor[0] = IndexShortLE();
			proj.ImpactFXArmor[1] = IndexShortLE();
			proj.ImpactFXArmor[2] = IndexShortLE();
			proj.ImpactFXArmor[3] = IndexShortLE();

			data.Data[p] = proj;
		}

		return data;
	}

	public override byte[]? Write(ProjectileData data) {

		using var outStream = new MemoryStream();

		void WriteShort(short s) {
			var b = WriteShortLE(s);
			outStream.Write(b, 0, b.Length);
		}

		WriteShort(data.Total);

		for (int p = 0; p < data.Data!.Length; p++) {
			var proj = data.Data[p];

			WriteShort(proj.Type!.Val);
			WriteShort(proj.MissileId);
			WriteShort(proj.DamageShield);
			WriteShort(proj.DamageArmor);
			WriteShort(proj.SplashFactor);
			WriteShort(proj.Speed);

			WriteShort(proj.ImpactFXShield[0]);
			WriteShort(proj.ImpactFXShield[1]);
			WriteShort(proj.ImpactFXShield[2]);
			WriteShort(proj.ImpactFXShield[3]);

			// this order is fairly wonky and probably requires more research.
			WriteShort(proj.ImpactFXGround[0]);
			WriteShort(proj.ImpactFXGround[1]);
			WriteShort(proj.ImpactFXGround[2]);
			WriteShort(proj.ImpactFXGround[3]);

			WriteShort(proj.ImpactFXArmor[0]);
			WriteShort(proj.ImpactFXArmor[1]);
			WriteShort(proj.ImpactFXArmor[2]);
			WriteShort(proj.ImpactFXArmor[3]);
		}

		return outStream.ToArray();
	}
}
