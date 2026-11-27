// Unity games: per-game edits of the Unity package's fixes.
//   data\unityfixes\<exe file name, lower case>\disable.txt
// Lines of disable.txt (empty lines and lines starting with '#' are skipped):
//   @include-off   turn off the Unity fix as a whole: every active "include = ShaderFixes\Unity*.ini" line of the
//                  game's d3dx.ini (the line UnitySwitch.ps1 has just switched on) becomes ";include = ..."
//   anything else  a regular expression; every active (not ';') line of the game's ShaderFixes\Unity_*.ini that
//                  matches is commented out
// Edited lines get Marker in front, so the edit is visible in the file and running it again changes nothing. It runs
// after UnitySwitch.ps1 on every apply, because a full install copies the package's files again and UnitySwitch.ps1
// sets the include line. Bytes other than the edited lines stay as they were.
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
        public const string IncludeOff = "@include-off";
        static readonly Encoding Raw = Encoding.GetEncoding(28591);   // Latin-1: every byte maps to one char and back
        static readonly Regex UnityInclude = new Regex(@"^\s*include\s*=\s*ShaderFixes[\\/]Unity[^\\/]*\.ini\s*$", RegexOptions.IgnoreCase);

        public static string RuleFile(string fixesRoot, string exeName)
        {
            if (string.IsNullOrEmpty(fixesRoot) || string.IsNullOrEmpty(exeName)) return null;
            string f = Path.Combine(fixesRoot, exeName.ToLowerInvariant(), "disable.txt");
            return File.Exists(f) ? f : null;
        }

        static IEnumerable<string> RuleLines(string ruleFile)
        {
            if (ruleFile == null) yield break;
            foreach (var l in File.ReadAllLines(ruleFile, Encoding.UTF8))
            {
                string t = l.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                yield return t;
            }
        }

        public static List<Regex> Rules(string ruleFile)
        {
            return RuleLines(ruleFile).Where(t => !t.StartsWith("@"))
                .Select(t => new Regex(t, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToList();
        }

        public static bool WantsIncludeOff(string ruleFile)
        {
            return RuleLines(ruleFile).Any(t => t.Equals(IncludeOff, StringComparison.OrdinalIgnoreCase));
        }

        // comments out the lines of file for which match is true (active lines only); returns how many.
        // plain: ";" + the line without leading blanks (the form UnitySwitch.ps1 reads back), else Marker + the line
        static int CommentOut(string file, Func<string, bool> match, bool plain = false)
        {
            var parts = Raw.GetString(File.ReadAllBytes(file)).Split('\n');
            int n = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                string line = parts[i].TrimEnd('\r');
                if (line.TrimStart().StartsWith(";") || !match(line)) continue;
                parts[i] = plain ? ";" + parts[i].TrimStart() : Marker + parts[i];
                n++;
            }
            if (n > 0) File.WriteAllBytes(file, Raw.GetBytes(string.Join("\n", parts)));
            return n;
        }

        // applies the game's rules to gameDir; returns the log lines
        public static List<string> Apply(string fixesRoot, string exeName, string gameDir)
        {
            var log = new List<string>();
            string rf = RuleFile(fixesRoot, exeName);
            if (rf == null) return log;
            var rules = Rules(rf);
            int total = 0;
            if (WantsIncludeOff(rf))
            {
                string dx = Path.Combine(gameDir, "d3dx.ini");
                // ";include = ..." and not Marker: UnitySwitch.ps1 only recognises that form, and on the next apply
                // it would otherwise add a second include line instead of switching this one back on
                int n = File.Exists(dx) ? CommentOut(dx, l => UnityInclude.IsMatch(l), true) : 0;
                if (n > 0) log.Add(Lang.T("  게임별 Unity 보정: d3dx.ini에서 Unity 픽스 include 끔"));
                total += n;
            }
            string sf = Path.Combine(gameDir, "ShaderFixes");
            if (rules.Count > 0 && Directory.Exists(sf))
                foreach (var ini in Directory.GetFiles(sf, "Unity_*.ini").OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                {
                    int n = CommentOut(ini, l => rules.Any(r => r.IsMatch(l)));
                    if (n == 0) continue;
                    log.Add(Lang.T("  게임별 Unity 보정: ") + Path.GetFileName(ini) + Lang.T("에서 ") + n + Lang.T("줄 끔"));
                    total += n;
                }
            if (total == 0) log.Add(Lang.T("  게임별 Unity 보정: 이미 적용되어 있음"));
            return log;
        }
    }
}
