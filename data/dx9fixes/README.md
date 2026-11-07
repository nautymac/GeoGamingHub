# DX9 per-game shader fixes (dgVoodoo2 + geo-11)

When a DirectX 8/9 game runs through dgVoodoo2 and geo-11, geo-11 sees the vertex shaders that dgVoodoo generates.
Some games draw their HUD with a perspective-style projection, so geo-11 gives each HUD element a different depth.
A fix here replaces those shaders for that one game.

Layout:

    data\dx9fixes\<exe file name, lower case>\<dgVoodoo2 version>\ShaderFixesDM\*.txt   (copied to the game's ShaderFixesDM)
    data\dx9fixes\<exe file name, lower case>\<dgVoodoo2 version>\ShaderFixes\*        (optional, copied to ShaderFixes)

geo-11 matches these files by shader hash, and the hash depends on the dgVoodoo2 build. The hub installs a fix only
when the dgVoodoo2 version chosen in the wizard matches the version folder; otherwise it logs that it skipped it.
Uninstall.bat removes the files together with geo-11.

## Making a fix

1. Install the game with the hub (DX9 kind). Keep `export_hlsl` and `export_shaders` at 0 in d3dx.ini: when they are 1,
   geo-11 decompiles and recompiles every shader and the game renders wrongly. Keep `dm_hud_detection = 0`.
2. In game, hunt the HUD vertex shader: NumPad 5 steps to the next vertex shader, NumPad 6 marks it.
   Marking writes `<hash>-vs.txt` (and `<hash>-vs_hud.txt`) to ShaderFixesDM. Press NumPad + to stop hunting.
3. Edit the assembly of `<hash>-vs.txt`. geo-11's stereo correction ends like this:

        add r3.x, -r4.y, r2.w
        mul r3.y, r4.x, r4.w
        mad r3.x, r3.x, r3.y, r2.x
        ne r3.y, l(1.000000), r2.w
        movc r2.x, r3.y, r3.x, r2.x

   Add a condition before the last `movc` that keeps the HUD at screen depth, then copy the file to `<hash>-vs_hud.txt`.
   F10 reloads the files while the game runs.
4. Put the edited files here under the exe name and the dgVoodoo2 version.

## Games

| exe | Game | dgVoodoo2 | Fix |
|---|---|---|---|
| bge.exe | Beyond Good & Evil | 2.79.1 | HUD at screen depth. The Jade engine draws its HUD with a projection whose Y scale (cb0[36].y) is negative, so `lt r3.z, cb0[36].y, l(0.0)` + `movc r3.x, r3.z, r2.x, r3.x` skips the stereo offset for those draws only. Shaders 9f21b5013d0c9a38, 381451ea83dd9de5, 34db16e593c9bc4a. |
