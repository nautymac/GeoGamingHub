// Self-tests of Geo Gaming Hub's Unity per-game edits; part of HubTest (partial class).
//   HubTest.exe <GeoGamingHub.exe> <out dir> unityunit
// unityunit: UnityFixOps.Apply on a temp folder with the repository's data\unityfixes (no game needed)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

static partial class HubTest
{
    static void UnityFixUnit(string app)
    {
        string root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(app)), "data", "unityfixes");
        Ok(Directory.Exists(root), "data/unityfixes exists next to the app");
        Ok(S("UnityFixOps", "RuleFile", root, "Heaven Dust 2.exe") != null, "Heaven Dust 2 rule file (exe name case ignored)");
        Ok(S("UnityFixOps", "RuleFile", root, "nogame.exe") == null, "no rule file for an unknown exe");

        string g = Path.Combine(outDir, "unitygame");
        if (Directory.Exists(g)) Directory.Delete(g, true);
        Directory.CreateDirectory(Path.Combine(g, "ShaderFixes"));
        string ini = Path.Combine(g, "ShaderFixes", "Unity_2019_1.ini");
        string orig = "[ShaderRegex9_VP_Source_cb1]\r\nshader_model = vs_4_0 vs_5_0\r\nrun = CustomShader_InvViewProj_VS\r\n" +
                      "ResourceInput_InvViewProj_VS=ref vs-cb1\r\n;run = CustomShader_InvViewProj_VS\r\n" +
                      "[ShaderRegex_CS]\nrun = CustomShader_InvViewProj2_CS\nrun = CustomShader_Other\n";
        File.WriteAllBytes(ini, Encoding.ASCII.GetBytes(orig));
        File.WriteAllText(Path.Combine(g, "ShaderFixes", "upscale.ini"), "run = CustomShader_InvViewProj_VS\r\n");

        var log = (List<string>)S("UnityFixOps", "Apply", root, "heaven dust 2.exe", g);
        string got = Encoding.ASCII.GetString(File.ReadAllBytes(ini));
        string m = (string)A.GetType("GeoGamingHub.UnityFixOps").GetField("Marker").GetValue(null);
        string want = orig.Replace("\r\nrun = CustomShader_InvViewProj_VS\r\n", "\r\n" + m + "run = CustomShader_InvViewProj_VS\r\n")
                          .Replace("\nrun = CustomShader_InvViewProj2_CS\n", "\n" + m + "run = CustomShader_InvViewProj2_CS\n");
        Eq(got, want, "matching active lines commented out, line endings and other lines kept");
        Ok(log.Count == 1 && log[0].Contains("Unity_2019_1.ini") && log[0].Contains("2"), "log names the file and the count");
        Eq(File.ReadAllText(Path.Combine(g, "ShaderFixes", "upscale.ini")), "run = CustomShader_InvViewProj_VS\r\n", "only Unity_*.ini is edited");

        log = (List<string>)S("UnityFixOps", "Apply", root, "heaven dust 2.exe", g);
        Eq(Encoding.ASCII.GetString(File.ReadAllBytes(ini)), want, "second run changes nothing");
        Eq(log.Count, 1, "second run: one 'already applied' line");
        Eq(((List<string>)S("UnityFixOps", "Apply", root, "nogame.exe", g)).Count, 0, "unknown exe: no log, no change");
    }
}
