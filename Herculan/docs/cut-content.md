# Cut and unreachable content

Retail content that was authored, shipped in the data files or the binary, and cannot be reached by playing the game. Two audiences: someone reading the data files who finds a weapon or a record that appears real and is not, and someone interested in what the game was going to have.

## Weapons

Three weapons appear in the `SHELL0/GAM` catalog with a code, a full name in `WEAPONS.BIN` and a price, but are not actually accessible in the campaign. Similar weapons later appeared in *Starsiege*.

- **`LAEW` — Locust Launcher** (id 26)
- **`MINE` — Mine Launcher** (id 27)
- **`MFAC` — MagnetoFusion Cannon** (id 28) is a different case from those two: its template is complete and it works, firing the Plasma Cannon's round (`PROJ.DAT` row 22) at nearly twice the range.

**`MFAC` is fitted to three chassis in `gam\trn_herc.dat`**, a nine-record stock-fit set that is otherwise a near-copy of the `ini_*.dat` files. It is the only retail data that arms a player machine with a cut weapon. No reader for that file has been traced in either binary, so it does not overturn the above — but a reader who finds `MFAC` there will reasonably think it does. See [`formats/herc-catalogs.md`](formats/herc-catalogs.md#gamtrn_hercdat--a-second-stock-fit-set).

Additionally, the game data includes an unused particle-beam weapon for the Cybrid Bull. The three Bull weapons (ids 19-21) are excluded twice over: they have no armory panel, and the thirty-entry weapon-class table at `0046f868` omits them, so a Bull weapon in a player hardpoint resolves to class `-1`. `damage.dat` also zeroes their scrap value where every other weapon's is its price divided by ten.

## Projectiles

- **A second, unused guided-projectile class.**

## Miscellaneous features

- **Last-known-position scanner blips.** The F4 scanner's hostile branch has a complete implementation of a blinking last-known-position marker, plotted on every other coarse tick. Every object constructor sets the byte that gates it and nothing ever clears it, and nothing writes the stored position either.
- **One-step capacitor recharge.** `WeaponMount_DemandFullCharge` (`0040f4f0`) fills a weapon capacitor to the 1200 maximum in a single call and is the obvious mechanism for a full-charge pickup or cheat. Its only caller is itself unreferenced anywhere in the image.
- Unused voiceover lines "Shutdown initiated" (CVM_0037.WAV) and "Powerup initiated. Internal damage detected." (CVM_0036.WAV) suggest that Dynamix considered mid-mission shutdown - a feature that was later implemented for stealth purposes in *Starsiege*.

## Files

- **`DEMO2.MSN`.** The one mission file of 62 that does not land on EOF cleanly — it undershoots by 42 bytes in the middle of a row's tail. A stale developer test file with a genuinely truncated tail, not a gap in the format. See [`formats/msn-mission-file.md`](formats/msn-mission-file.md).
