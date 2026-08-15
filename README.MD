# The HERCULAN Engine

The HERCULAN Engine is an effort to fully reverse-engineer the classic video game _[Earthsiege 2](https://en.wikipedia.org/wiki/Earthsiege_2)_ (developed by Dynamix and published by Sierra in 1996) and port it to modern operating systems. This engine is still in the very early stages of development; there is no specific development timetable.

## HercWorks CSharp

A fundemental feature of the HERCULAN Engine is HercWorks CSharp, a toolset for viewing (and sometimes editing) data files from EarthSiege 2. HercWorks CSharp is a rewrite and extension of [herc-works-mdk](https://github.com/Subject9x/herc-works-mdk), originally by Subject9x. HercWorks has been rewritten in C#/.NET. HercWorks CSharp builds upon the original Java library, fixing bugs, expanding the scope of the reverse-engineering work, and adding a graphical user interface built with Windows Forms. Unlike the original Java library, this repository currently does not support exporting data to JSON.

My great thanks to Subject9x (developer of the original repository) and any contributors (e.g. the Starsiege Community) for the initial reverse-engineering work that has made this possible. Also, I apologize for taking their work and continuing it in a completely different programming language!

## The engine

The HERCULAN Engine is intended to eventually be a playable cross-platform modern port of _Earthsiege 2_. Unlike many classic games which have been ported to modern operating systems, such as _Doom_, the source code for Earthsiege 2 has never been released. This means the HERCULAN Engine is not simply a matter of taking the original code and updating it for modern systems; instead, the game logic in its entirety has to be reconstructed through reverse-engineering.

### Using the engine

Earthsiege 2 is a copyrighted work. While a legal owner of the copyright has previously released the game for free, I do not have a license to distribute the original game files and they are not included in this repository. To use the HERCULAN Engine, you will need a copy of the original game files, which you can acquire from abandonware websites or by purchasing an original retail CD-ROM. I am not aware of any digital marketplace that sells Earthsiege 2.

## Screenshots

<div align="center">
  <figure>
    <img src="Images/VOL%20Browser.png" alt="VOL Browser" width="400">
    <p><em>HercWorks UI for browsing VOL archives</em></p>
  </figure>
</div>

<div align="center">
  <figure>
    <img src="Images/Campaign%20Resources%20Editor.png" alt="Campaign Resources Editor" width="400">
    <p><em>Campaign Resources Editor</em></p>
  </figure>
</div>

<div align="center">
  <figure>
    <img src="Images/DTS%20Viewer.png" alt="DTS Viewer" width="400">
    <p><em>DTS (3D model) Viewer</em></p>
  </figure>
</div>

<div align="center">
  <figure>
    <img src="Images/Herc%20Stats%20Editor.png" alt="Herc Stats Editor" width="400">
    <p><em>Herc Stats Editor</em></p>
  </figure>
</div>

<div align="center">
  <figure>
    <img src="Images/Texture%20Viewer.png" alt="Texture Viewer" width="400">
    <p><em>Texture Viewer</em></p>
  </figure>
</div>

<div align="center">
  <figure>
    <img src="Images/Mission%20Editor.png" alt="Mission Editor" width="400">
    <p><em>An early version of the HERCULAN Engine, displaying static content and a basic debugging interface that may eventually turn into a mission editor (no promises!)</em></p>
  </figure>
</div>

## Project Structure

- **`Herculan/`** — Main project directory
  - **`HercWorksMDK.sln`** — Visual Studio solution for HercWorks CSharp (WinForms tool)
  - **`HerculanEngine.sln`** — Visual Studio solution for the HERCULAN Engine
  - **`src/`** — Source code for HercWorks CSharp and the HERCULAN Engine
  - **`docs/`** — Technical documentation on file formats, reverse-engineering findings, and architecture
- **`tools/`** — Utility scripts and plugins (e.g., Ghidra scripts for binary analysis)
- **`Images/`** — Screenshots of the HercWorks UI and engine

For details on specific reverse-engineering work and file format documentation, see the files in `Herculan/docs/`.

## AI Disclosure
Development of HercWorks CSharp has been largely powered by Claude (with prompting, testing, and feedback from a human). AI-generated documentation and code may contain mistakes. See the accompanying LICENSE file for copyright information.

## License

See the accompanying LICENSE file.