namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Possibly top-level abstract class for a certain set of observed map objects. Some map
/// objects have a GUID that seems to be counting up.
/// Ported from org.hercworks.core.data.file.msn.MapObject.
/// </summary>
public abstract class MapObject {
	public short GUID { get; set; }
}
