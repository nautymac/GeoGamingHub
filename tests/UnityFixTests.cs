// Self-tests of Geo Gaming Hub's Unity per-game edits; part of HubTest (partial class).
//   HubTest.exe <GeoGamingHub.exe> <out dir> unityunit
// unityunit: UnityFixOps.Apply on temp folders: line rules from a temp rule folder, @include-off with the repository's
//            data\unityfixes (Heaven Dust 2) (no game needed)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

static partial class HubTest
{
    static void UnityFixUnit(string app)
    {
        string root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(app)), "data", "unityfixes");
        Ok(Directory.Exists(root), "data/unityfixes exists next to the app");
        string hd2 = (string)S("UnityFixOps", "RuleFile", root, "Heaven Dust 2.exe");
        Ok(hd2 != null, "Heaven Dust 2 rule file (exe name case ignored)");
        Ok((bool)S("UnityFixOps", "WantsIncludeOff", hd2), "Heaven Dust 2: @include-off");
        Ok(S("UnityFixOps", "RuleFile", root, "nogame.exe") == null, "no rule file for an unknown exe");
        string m = (string)A.GetType("GeoGamingHub.UnityFixOps").GetField("Marker").GetValue(null);

        // line rules (temp rule folder)
        string rules = Path.Combine(outDir, "unityrules");
        if (Directory.Exists(rules)) Directory.Delete(rules, true);
        Directory.CreateDirectory(Path.Combine(rules, "linegame.exe"));
        File.WriteAllText(Path.Combine(rules, "linegame.exe", "disable.txt"), "# comment\n\n^\\s*run\\s*=\\s*CustomShader_InvViewProj\n");
        string g = Path.Combine(outDir, "unitygame");
        if (Directory.Exists(g)) Directory.Delete(g, true);
        Directory.CreateDirectory(Path.Combine(g, "ShaderFixes"));
        string ini = Path.Combine(g, "ShaderFixes", "Unity_2019_1.ini");
        string orig = "[ShaderRegex9_VP_Source_cb1]\r\nshader_model = vs_4_0 vs_5_0\r\nrun = CustomShader_InvViewProj_VS\r\n" +
                      "ResourceInput_InvViewProj_VS=ref vs-cb1\r\n;run = CustomShader_InvViewProj_VS\r\n" +
                      "[ShaderRegex_CS]\nrun = CustomShader_InvViewProj2_CS\nrun = CustomShader_Other\n";
        File.WriteAllBytes(ini, Encoding.ASCII.GetBytes(orig));
        File.WriteAllText(Path.Combine(g, "ShaderFixes", "upscale.ini"), "run = CustomShader_InvViewProj_VS\r\n");
        var log = (List<string>)S("UnityFixOps", "Apply", rules, "LineGame.exe", g);
        string want = orig.Replace("\r\nrun = CustomShader_InvViewProj_VS\r\n", "\r\n" + m + "run = CustomShader_InvViewProj_VS\r\n")
                          .Replace("\nrun = CustomShader_InvViewProj2_CS\n", "\n" + m + "run = CustomShader_InvViewProj2_CS\n");
        Eq(Encoding.ASCII.GetString(File.ReadAllBytes(ini)), want, "line rule: matching active lines commented out, line endings and other lines kept");
        Ok(log.Count == 1 && log[0].Contains("Unity_2019_1.ini") && log[0].Contains("2"), "line rule: log names the file and the count");
        Eq(File.ReadAllText(Path.Combine(g, "ShaderFixes", "upscale.ini")), "run = CustomShader_InvViewProj_VS\r\n", "line rule: only Unity_*.ini is edited");
        log = (List<string>)S("UnityFixOps", "Apply", rules, "linegame.exe", g);
        Eq(Encoding.ASCII.GetString(File.ReadAllBytes(ini)), want, "line rule: second run changes nothing");
        Eq(log.Count, 1, "line rule: second run logs 'already applied'");
        Eq(((List<string>)S("UnityFixOps", "Apply", rules, "nogame.exe", g)).Count, 0, "unknown exe: no log, no change");

        // @include-off (Heaven Dust 2 from the repository's data)
        string g2 = Path.Combine(outDir, "unitygame2");
        if (Directory.Exists(g2)) Directory.Delete(g2, true);
        Directory.CreateDirectory(Path.Combine(g2, "ShaderFixes"));
        string u2 = Path.Combine(g2, "ShaderFixes", "Unity_2019_1.ini");
        File.WriteAllText(u2, "run = CustomShader_InvViewProj_VS\r\n");
        string dx = Path.Combine(g2, "d3dx.ini");
        string dxOrig = "[Include]\r\ninclude = ShaderFixes\\upscale.ini\r\n;include = ShaderFixes\\mouse.ini\r\ninclude = ShaderFixes\\Unity_2019_1.ini\r\n;include = ShaderFixes\\Unity55_DHR.ini\r\n[Device]\r\nupscaling=1\r\n";
        File.WriteAllText(dx, dxOrig);
        log = (List<string>)S("UnityFixOps", "Apply", root, "Heaven Dust 2.exe", g2);
        string dxWant = dxOrig.Replace("\r\ninclude = ShaderFixes\\Unity_2019_1.ini\r\n", "\r\n;include = ShaderFixes\\Unity_2019_1.ini\r\n");
        Eq(File.ReadAllText(dx), dxWant, "@include-off: only the Unity include line turned off, as ';include = ...'");
        Ok(log.Count == 1 && log[0].Contains("d3dx.ini"), "@include-off: one log line naming d3dx.ini");
        Eq(File.ReadAllText(u2), "run = CustomShader_InvViewProj_VS\r\n", "@include-off: Unity_*.ini untouched (no line rules)");
        // UnitySwitch.ps1's include pattern must still find the line, so a re-apply switches it on instead of adding one
        var us = new Regex(@"^\s*;*\s*include\s*=\s*ShaderFixes\\(Unity55_DHR\.ini|Unity56_DHR\.ini|Unity20171_DHR\.ini|Unity_2019_1\.ini)\s*$");
        Eq(File.ReadAllLines(dx).Count(l => us.IsMatch(l)), 2, "@include-off: UnitySwitch.ps1 still sees both Unity include lines");
        log = (List<string>)S("UnityFixOps", "Apply", root, "heaven dust 2.exe", g2);
        Eq(File.ReadAllText(dx), dxWant, "@include-off: second run changes nothing");
        // re-apply: UnitySwitch switches the line back on, the rule turns it off again
        File.WriteAllText(dx, dxOrig);
        S("UnityFixOps", "Apply", root, "heaven dust 2.exe", g2);
        Eq(File.ReadAllText(dx), dxWant, "@include-off: after UnitySwitch switched it on again, same result");
    }
}
