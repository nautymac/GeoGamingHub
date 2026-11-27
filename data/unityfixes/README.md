# Unity per-game edits (geo-11 Unity package)

The Unity package's `ShaderFixes\Unity_*.ini` fixes lights, shadows and similar effects for all Unity games. Some
games crash or render wrongly with it, or with one part of it. A rule file here turns that off for one game.

Layout:

    data\unityfixes\<exe file name, lower case>\disable.txt

Lines of `disable.txt` (empty lines and lines starting with `#` are skipped):

- `@include-off`: the Unity fix is turned off as a whole. The active `include = ShaderFixes\Unity*.ini` line of the
  game's d3dx.ini becomes `;include = ...`, the form `UnitySwitch.ps1` switches back on, so a later apply does not
  add a second include line.
- any other line: a regular expression (case ignored). Every active line of the game's `ShaderFixes\Unity_*.ini`
  that matches is commented out with `; [GeoGamingHub unityfix] ` in front.

The hub applies the rules on every Unity apply (full install or re-apply), after `UnitySwitch.ps1`. Applying again
changes nothing. Uninstall.bat removes ShaderFixes and d3dx.ini together with geo-11.

Check a rule in game past the menu and from empty shader caches (delete ShaderCache and ShaderCacheDM, as a full
install does): Heaven Dust 2 with only part of the fix off reached the menu and crashed once a game was started.

## Games

| exe | Game | GPU tested | Rule | Why |
|---|---|---|---|---|
| heaven dust 2.exe | Heaven Dust 2 | Intel Arc 140V (driver 32.0.101.8991) | `@include-off` | With Unity_2019_1.ini on, the game crashes on its first frame (access violation in d3d11.dll `CSSetConstantBuffers`, called from geo-11 DirectMode while it runs the inverse-matrix compute shaders). With only those compute shaders off it crashes in the Intel driver (igd10umt64xe worker thread) once a game is started, often already at the menu on a first start after a fresh install. With the fix off it runs, also from empty shader caches. |
