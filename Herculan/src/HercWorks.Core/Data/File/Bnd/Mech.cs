using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Bnd;

/// <summary>
/// FILE - SIMVOL0\BND\MECH.BND — a 394-byte record whose layout is open. Content offsets 0-15 read
/// 242, 164, 51, 49, 12, 0, 42, 0, 48, 117, 0, 0, 100, 0, 100, 0; from about offset 8 it looks like a
/// per-mech-type array. Not modelled: <c>.BND</c> is a build-time source format DBSIM never opens.
/// See <c>docs/formats/bnd-notes.md#open</c>.
/// </summary>
public class Mech : DataFile {
}
