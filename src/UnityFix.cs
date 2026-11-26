// Unity games: per-game edits of the Unity package's ShaderFixes\Unity_*.ini.
//   data\unityfixes\<exe file name, lower case>\disable.txt
// Each non-empty line of disable.txt that does not start with '#' is a regular expression. Every active (not ';')
// line of the game's ShaderFixes\Unity_*.ini that matches one of them is commented out with Marker in front, so the
// edit is visible in the file and running it again changes nothing. It runs after UnitySwitch.ps1 on every apply,
// because a full install copies the package's Unity_*.ini again. Bytes other than the edited lines stay as they were.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GeoGamingHub
{
    static class UnityFixOps
    {
        public const string Marker = "; [GeoGamingHub unityfix] ";
        static readonly Encoding Raw = Encoding.GetEncoding(28591);   // Latin-1: every byte maps to one char and back

        public static string RuleFile(string fixesRoot, string exeName)
        {
            if (string.IsNullOrEmpty(fixesRoot) || string.IsNullOrEmpty(exeName)) return null;
            string f = Path.Combine(fixesRoot, exeName.ToLowerInvariant(), "disable.txt");
            return File.Exists(f) ? f : null;
        }

        public static List<Regex> Rules(string ruleFile)
        {
            var list = new List<Regex>();
            if (ruleFile == null) return list;
            foreach (var l in File.ReadAllLines(ruleFile, Encoding.UTF8))
            {
                string t = l.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                list.Add(new Regex(t, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            }
            return list;
        }

        // comments out the matching lines in gameDir\ShaderFixes\Unity_*.ini; returns the log lines
        public static List<string> Apply(string fixesRoot, string exeName, string gameDir)
        {
            var log = new List<string>();
            var rules = Rules(RuleFile(fixesRoot, exeName));
            if (rules.Count == 0) return log;
            string sf = Path.Combine(gameDir, "ShaderFixes");
            if (!Directory.Exists(sf)) return log;
            int total = 0;
            foreach (var ini in Directory.GetFiles(sf, "Unity_*.ini").OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                string text = Raw.GetString(File.ReadAllBytes(ini));
                var parts = text.Split('\n');
                int n = 0;
                for (int i = 0; i < parts.Length; i++)
                {
                    string line = parts[i].TrimEnd('\r');
                    if (line.TrimStart().StartsWith(";")) continue;
                    if (!rules.Any(r => r.IsMatch(line))) continue;
                    parts[i] = Marker + parts[i];
                    n++;
                }
                if (n == 0) continue;
                File.WriteAllBytes(ini, Raw.GetBytes(string.Join("\n", parts)));
                log.Add(Lang.T("  게임별 Unity 보정: ") + Path.GetFileName(ini) + Lang.T("에서 ") + n + Lang.T("줄 끔"));
                total += n;
            }
            if (total == 0) log.Add(Lang.T("  게임별 Unity 보정: 이미 적용되어 있음"));
            return log;
        }
    }
}
