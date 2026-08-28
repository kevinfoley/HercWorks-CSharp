using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Core.Data.Struct;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>Ported from org.hercworks.core.io.transform.dbsim.PaperDiagramGraphTransformer.</summary>
public class PaperDiagramGraphTransformer : ByteTransformer<PaperDollGraphic> {
	public override PaperDollGraphic? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var pdg = new PaperDollGraphic();

		pdg.TotalViews = IndexIntLE();
		pdg.Entries = new PaperDollGraphic.ViewEntry[pdg.TotalViews];

		// Structure Damage view
		var structure = pdg.NewViewEntry();
		structure.Origin = new PixelPoint(IndexIntLE(), IndexIntLE());
		structure.Size = new PixelPoint(IndexIntLE(), IndexIntLE());
		structure.Regions = new PaperDollGraphic.ViewRegion[IndexIntLE()];
		for (int r = 0; r < structure.Regions.Length; r++) {
			var region = pdg.NewViewRegion();
			region.Index = IndexIntLE();
			region.TopLeft = new PixelPoint(IndexIntLE(), IndexIntLE());
			region.BottomRight = new PixelPoint(IndexIntLE(), IndexIntLE());
			region.Unk_val = IndexIntLE();
			region.Spacer = IndexIntLE();
			structure.Regions[r] = region;
		}
		pdg.Entries[0] = structure;

		// Internal Damage view
		var internals = pdg.NewViewEntry();
		internals.Origin = new PixelPoint(IndexIntLE(), IndexIntLE());
		internals.Size = new PixelPoint(IndexIntLE(), IndexIntLE());
		internals.Regions = new PaperDollGraphic.ViewRegion[IndexIntLE()];
		for (int r = 0; r < internals.Regions.Length; r++) {
			var region = pdg.NewViewRegion();
			region.Index = IndexIntLE();
			region.TopLeft = new PixelPoint(IndexIntLE(), IndexIntLE());
			region.BottomRight = new PixelPoint(IndexIntLE(), IndexIntLE());
			region.Unk_val = IndexIntLE();
			region.Spacer = IndexIntLE();
			internals.Regions[r] = region;
		}
		pdg.Entries[1] = internals;

		// HUD-F5 Target view
		var hudTarget = pdg.NewViewEntry();
		hudTarget.Origin = new PixelPoint(IndexIntLE(), IndexIntLE());
		hudTarget.Size = new PixelPoint(IndexIntLE(), IndexIntLE());
		hudTarget.Regions = new PaperDollGraphic.ViewRegion[IndexIntLE()];
		for (int r = 0; r < hudTarget.Regions.Length; r++) {
			var region = pdg.NewViewRegion();
			region.Index = IndexIntLE();
			region.TopLeft = new PixelPoint(IndexIntLE(), IndexIntLE());
			region.BottomRight = new PixelPoint(IndexIntLE(), IndexIntLE());
			region.Unk_val = IndexIntLE();
			region.Spacer = IndexIntLE();
			hudTarget.Regions[r] = region;
		}
		pdg.Entries[2] = hudTarget;

		pdg.Hardpoints = new PaperDollGraphic.HardpointEntry[IndexIntLE()];
		for (int h = 0; h < pdg.Hardpoints.Length; h++) {
			var hardpoint = pdg.NewHardpointEntry();
			hardpoint.Origin = new PixelPoint(IndexIntLE(), IndexIntLE());
			hardpoint.Unk1 = IndexIntLE();
			hardpoint.Unk2 = IndexIntLE();
			hardpoint.Spacer = IndexIntLE();
			pdg.Hardpoints[h] = hardpoint;
		}

		return pdg;
	}

	public override byte[]? Write(PaperDollGraphic pdg) {

		using var outStream = new MemoryStream();

		void WriteInt(int i) {
			var b = WriteIntLE(i);
			outStream.Write(b, 0, b.Length);
		}

		WriteInt(pdg.TotalViews);

		for (int v = 0; v < pdg.Entries!.Length; v++) {
			var view = pdg.Entries[v];

			WriteInt(view.Origin.X);
			WriteInt(view.Origin.Y);
			WriteInt(view.Size.X);
			WriteInt(view.Size.Y);
			WriteInt(view.Regions!.Length);

			for (int r = 0; r < view.Regions.Length; r++) {
				var region = view.Regions[r];
				WriteInt(region.Index);
				WriteInt(region.TopLeft.X);
				WriteInt(region.TopLeft.Y);
				WriteInt(region.BottomRight.X);
				WriteInt(region.BottomRight.Y);
				WriteInt(region.Unk_val);
				WriteInt(region.Spacer);
			}
		}

		WriteInt(pdg.Hardpoints!.Length);
		for (int h = 0; h < pdg.Hardpoints.Length; h++) {
			var hpoint = pdg.Hardpoints[h];
			WriteInt(hpoint.Origin.X);
			WriteInt(hpoint.Origin.Y);
			WriteInt(hpoint.Unk1);
			WriteInt(hpoint.Unk2);
			WriteInt(hpoint.Spacer);
		}

		return outStream.ToArray();
	}
}
