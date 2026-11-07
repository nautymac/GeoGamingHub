// Self-tests of Geo Gaming Hub's DX9 per-game fixes and safe settings; part of HubTest (partial class).
//   HubTest.exe <GeoGamingHub.exe> <out dir> dx9unit
// dx9unit: Dx9Ops.InstallFixes / GuardSettings on temp folders and the repository's data\dx9fixes (no game needed)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static partial class HubTest
{
    static void Dx9Unit(string app)
    {
        string root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(app)), "data", "dx9fixes");
        Ok(Directory.Exists(root), "data/dx9fixes exists next to the app");
        var vers = (string[])S("Dx9Ops", "FixVersions", root, "BGE.exe");
        Eq(string.Join(",", vers), "2.79.1", "BGE.exe fix versions (exe name case ignored)");
        Eq(((string[])S("Dx9Ops", "FixVersions", root, "nogame.exe")).Length, 0, "no fix for an unknown exe");
        Eq(((string[])S("Dx9Ops", "FixVersions", null, "BGE.exe")).Length, 0, "no data folder -> no fix");

        string g = Path.Combine(outDir, "dx9game");
        if (Directory.Exists(g)) Directory.Delete(g, true);
        Directory.CreateDirectory(Path.Combine(g, "ShaderFixesDM"));
        File.WriteAllText(Path.Combine(g, "ShaderFixesDM", "hotkeys.ini"), "[KeyToggleStereo]\r\n");

        var log = (List<string>)S("Dx9Ops", "InstallFixes", root, "BGE.exe", "2.86.5", g);
        Ok(log.Count == 1 && log[0].Contains("2.79.1"), "other dgVoodoo version: skipped with a log line");
        Eq(Directory.GetFiles(Path.Combine(g, "ShaderFixesDM")).Length, 1, "other dgVoodoo version: nothing copied");

        log = (List<string>)S("Dx9Ops", "InstallFixes", root, "BGE.exe", "2.79.1", g);
        var files = Directory.GetFiles(Path.Combine(g, "ShaderFixesDM")).Select(Path.GetFileName).ToList();
        Eq(files.Count, 7, "matching version: 6 fix files + the existing hotkeys.ini");
        Ok(files.Contains("9f21b5013d0c9a38-vs.txt") && files.Contains("9f21b5013d0c9a38-vs_hud.txt"), "matching version: HUD shader files copied");
        Ok(File.ReadAllText(Path.Combine(g, "ShaderFixesDM", "381451ea83dd9de5-vs.txt")).Contains("lt r3.z, cb0[36].y"), "copied file carries the HUD condition");

        string dx = Path.Combine(g, "d3dx.ini"), dm = Path.Combine(g, "d3dxdm.ini");
        File.WriteAllText(dx, "[Logging]\r\ncalls=1\r\n[Rendering]\r\nexport_shaders=1\r\nexport_hlsl=0\r\n");
        File.WriteAllText(dm, "[Stereo]\r\ndm_hud_detection = 1\r\ndm_separation = 80\r\n");
        log = (List<string>)S("Dx9Ops", "GuardSettings", g);
        Eq(S("Ini", "Get", dx, "calls"), "0", "guard: calls=0");
        Eq(S("Ini", "Get", dx, "export_shaders"), "0", "guard: export_shaders=0");
        Eq(S("Ini", "Get", dx, "export_hlsl"), "0", "guard: export_hlsl stays 0");
        Eq(S("Ini", "Get", dm, "dm_hud_detection"), "0", "guard: dm_hud_detection=0");
        Eq(S("Ini", "Get", dm, "dm_separation"), "80", "guard: other values untouched");
        Ok(log.Count == 1 && !log[0].Contains("export_hlsl"), "guard: logs only what it changed");
        Eq(((List<string>)S("Dx9Ops", "GuardSettings", g)).Count, 0, "guard: second run changes nothing");
    }
}
