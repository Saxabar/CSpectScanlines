# CSpect Scanlines

A plugin for the [CSpect](https://mdf200.itch.io/cspect) ZX Spectrum Next emulator by Mike Dailly
that draws TV-style scanlines over the emulator window.

CSpect 3 no longer has a TV shader: `+tv` is still listed in its ReadMe, but the shader has been
disabled for some time, so the option does nothing. This plugin brings
the scanlines back without using the emulator's renderer: it draws the lines in a transparent,
click-through window kept exactly over the CSpect window, and it uses `+tv` as its on switch.

## Installation

1. Download `Scanlines.dll` from the [Releases](../../releases) page (or build it, see below).
2. Copy it into the CSpect folder, next to `CSpect.exe`.
3. Start CSpect with `+tv`, for example:

   ```
   CSpect.exe -w3 -vsync -zxnext -mmc=.\ +tv game.nex
   ```

When the plugin is loaded, the CSpect console shows `Scanlines added (on)`.

## Keys

While the CSpect window has the focus:

| Keys | Action |
|---|---|
| Ctrl+F1 | scanlines on/off |
| Ctrl+PageUp | darker lines |
| Ctrl+PageDown | lighter lines |

## How the lines are drawn

For every line of the emulated display (256 lines, border included) the lower half of its pixels is
darkened: 1 pixel out of 2 at `-w2`, 1 out of 3 at `-w3`, 2 out of 4 at `-w4`. At `-w1` there is no
room for lines and nothing is drawn.

## Limits

- Windows only: on Linux and macOS CSpect runs with Mono and the transparent window does not work.
- In full screen the lines may not appear if CSpect takes the screen in exclusive mode.
- The plugin is active for every game started with `+tv`; remove `Scanlines.dll` to uninstall it.

## Building

`build.bat` compiles the plugin with the C# compiler included in Windows (.NET Framework 4); it needs
`Plugin.dll` from the CSpect folder:

```
build.bat "C:\path\to\CSpect"
```

The result is `bin\Scanlines.dll`.

## License

GPL-3.0, see [LICENSE](LICENSE). The CSpect plugin interface (`Plugin.dll`) is by Mike Dailly,
MIT licensed: https://github.com/mikedailly/CSpectPlugins
