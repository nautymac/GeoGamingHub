# Unity per-game edits (geo-11 Unity package)

The Unity package's `ShaderFixes\Unity_*.ini` fixes lights, shadows and similar effects for all Unity games. Some
games crash or render wrongly with one part of it. A rule file here turns off just that part for one game.

Layout:

    data\unityfixes\<exe file name, lower case>\disable.txt

Each line of `disable.txt` that is not empty and does not start with `#` is a regular expression (case ignored).
When the hub applies a Unity install (full install or re-apply), it runs `UnitySwitch.ps1` first and then comments
out every active line of the game's `ShaderFixes\Unity_*.ini` that matches, writing `; [GeoGamingHub unityfix] `
in front of it. Applying again changes nothing. Uninstall.bat removes ShaderFixes together with geo-11.

## Games

| exe | Game | GPU tested | Lines turned off | Why |
|---|---|---|---|---|
| heaven dust 2.exe | Heaven Dust 2 | Intel Arc 140V (driver 32.0.101.8991) | `run = CustomShader_InvViewProj*` (8 lines) | The inverse-matrix compute shaders crash the game on its first frame: access violation in d3d11.dll `CSSetConstantBuffers` called from geo-11 DirectMode. Bisected in game: any of `Dispatch`, or the input and output bindings together, crash; with the `run` lines off the game starts and the other patches stay on. |
