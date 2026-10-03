# Handoff: runtime language selection

Agreed design for letting the player change language from HERCULAN's own UI.

Archives now mount as retail does (`GameContent.MountSimulator`/`MountShell`), so language detection runs over `MountedArchives`: a v1.10 install holds only its own `SIMVOIC?.VOL` and mounts the other two from the disc.

Test data: `ES2v110\CD\` (disc) and `ES2v110\Install-ENG|FRE|GER\` (Win95 Maximum installs) and `Install-ENG-Min\` (Minimum), each with `drive.cfg` → `CD\`, built by `tools/scripts/es2_build_v110_installs.py`.

## Language plumbing

1. One retail language value, read from `data\language.cfg` (the Unported item in `retail-builds.md`). The Settings menu writes the choice back there, as one of the letters retail reads: `E`, `F` or `G` (VSHELL's manual open exits on any other letter). The user wants every file retail writes (saves included) written to the install as retail does.
2. Every reader takes it: `ShellText.Load` (`LANG0.VOL` folder, no more ENG/FRE/GER fallthrough), mission text extension (`ShellTrainingLaunch`), `SIMLANG.VOL` `st<letter>\`, the voice folder (`ComputerVoice.ResourceFolder`, and the instructor's `audio.InstructorClipPath` in `Program.cs`, both English now), `OnlineManual` (effective language, not the file), and `RunMission` (retail passes `-F`/`-G` to DBSIM separately). `campaign.str`'s folder is v1.10-only VSHELL code, not in Ghidra: stays Unported. The shell's language is also prefs.cfg option 43 (`ShellOption_Language`, VSHELL `004824e3`); reconcile that with `command-line.md`'s switches before porting.
3. The Settings menu offers a retail language when its folder (`ENGLISH\`, `FRENCH\`, `GERMAN\`) is on the disc or in the install, as the user chose. That is all a v1.0 language changes (the manual and readme): v1.0's `SIMVOICF/G.VOL` and `LANG0.VOL` `FRE\`/`GER\` are English copies. The readers in step 2 must therefore not assume a translation exists for every offered language; translated content is `SIMLANG.VOL` `STF\`/`STG\` and `.FRE`/`.GER` mission text. Spanish (DBSIM `-E`) has no data in any build.

## UI

- `LocalizationTable`: at startup, fall back to the last valid choice (store it beside `selected-locale.txt`'s value) before `en`.
- Cyrillic and other scripts need glyph ranges in the ImGui font atlas (`Program.cs` builds it with defaults); do this only when a `.lang` needs it.
