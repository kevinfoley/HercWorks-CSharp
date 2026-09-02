# Cut and unreachable content

Retail content that was authored, shipped in the data files or the binary, and cannot be reached by
playing the game. Two audiences: someone reading the data files who finds a weapon or a record that
appears real and is not, and someone interested in what the game was going to have.

## Weapons

Three weapons appear in the `SHELL0/GAM` catalog with a code, a full name in `WEAPONS.BIN` and a
price, but are not actually accessible in the campaign. Similar weapons later appeared in
Starsiege.

- **`LAEW` — Locust Launcher** (id 26)
- **`MINE` — Mine Launcher** (id 27)
- **`MFAC` — MagnetoFusion Cannon** (id 28) is a different case from those two: its template is
  complete and it works, firing the Plasma Cannon's round (`PROJ.DAT` row 22) at nearly twice the
  range.

Additionally, the game data includes an unused particle-beam weapon for the Cybrid Bull.

## Projectiles

- **A second, unused guided-projectile class.**

## Miscellaneous features

- **Last-known-position scanner blips.** The F4 scanner's hostile branch has a complete
  implementation of a blinking last-known-position marker, plotted on every other coarse tick. Every
  object constructor sets the byte that gates it and nothing ever clears it, and nothing writes the
  stored position either.
- **One-step capacitor recharge.** `WeaponMount_DemandFullCharge` (`0040f4f0`) fills a weapon
  capacitor to the 1200 maximum in a single call and is the obvious mechanism for a full-charge
  pickup or cheat. Its only caller is itself unreferenced anywhere in the image.

## Files

- **`DEMO2.MSN`.** The one mission file of 62 that does not land on EOF cleanly — it undershoots by
  42 bytes in the middle of a row's tail. A stale developer test file with a genuinely truncated
  tail, not a gap in the format. See [`formats/msn-mission-file.md`](formats/msn-mission-file.md).
