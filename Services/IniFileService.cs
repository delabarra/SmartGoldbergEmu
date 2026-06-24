using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class IniFileService
    {
        public IniFile ParseFile(string filePath)
        {
            var iniFile = new IniFile();
            if (!File.Exists(filePath))
                return iniFile;

            string currentSection = null;
            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                var line = new IniLine { RawLine = rawLine };
                var t = rawLine.Trim();

                if (string.IsNullOrEmpty(t))
                    line.Type = IniLineType.Empty;
                else if (t[0] == ';' || t[0] == '#')
                    line.Type = IniLineType.Comment;
                else if (t.Length >= 2 && t[0] == '[' && t[t.Length - 1] == ']')
                {
                    line.Type = IniLineType.Section;
                    currentSection = t.Substring(1, t.Length - 2);
                    line.Section = currentSection;
                }
                else
                {
                    var eq = t.IndexOf('=');
                    if (eq >= 0)
                    {
                        line.Type = IniLineType.KeyValue;
                        line.Section = currentSection;
                        line.Key = t.Substring(0, eq).Trim();
                        line.Value = eq + 1 < t.Length ? t.Substring(eq + 1).Trim() : string.Empty;
                    }
                    else
                        line.Type = IniLineType.Comment;
                }

                iniFile.Lines.Add(line);
            }

            return iniFile;
        }

        public void SetValue(IniFile iniFile, string section, string key, string value, bool skipIfDefault = false)
        {
            var lines = iniFile.Lines;
            var existing = lines.FirstOrDefault(l => MatchesKey(l, section, key));

            if (string.IsNullOrEmpty(value) && skipIfDefault)
            {
                if (existing != null)
                    lines.Remove(existing);
                return;
            }

            var v = value ?? string.Empty;
            if (existing != null)
            {
                existing.Value = v;
                existing.RawLine = key + "=" + v;
                return;
            }

            var sectionIndex = lines.FindIndex(l => l.Type == IniLineType.Section && l.Section == section);
            if (sectionIndex >= 0)
                lines.Insert(FindKeyInsertIndex(lines, sectionIndex), KeyValueLine(section, key, v));
            else
                AppendSectionWithKey(lines, section, key, v);
        }

        public void WriteFile(IniFile iniFile, string filePath)
        {
            File.WriteAllLines(filePath, iniFile.Lines.Select(l => l.RawLine));
        }

        public string GetValue(IniFile iniFile, string section, string key)
        {
            var line = iniFile.Lines.FirstOrDefault(l => MatchesKey(l, section, key));
            return line?.Value;
        }

        public bool RemoveValue(IniFile iniFile, string section, string key)
        {
            if (iniFile == null)
                return false;

            var lines = iniFile.Lines;
            var existing = lines.FirstOrDefault(l => MatchesKey(l, section, key));
            if (existing == null)
                return false;

            lines.Remove(existing);
            RemoveSectionIfEmpty(lines, section);
            return true;
        }

        private static bool MatchesKey(IniLine l, string section, string key)
        {
            return l.Type == IniLineType.KeyValue &&
                   l.Section == section &&
                   l.Key.Equals(key, StringComparison.OrdinalIgnoreCase);
        }

        private static IniLine KeyValueLine(string section, string key, string value)
        {
            return new IniLine
            {
                Type = IniLineType.KeyValue,
                Section = section,
                Key = key,
                Value = value,
                RawLine = key + "=" + value
            };
        }

        private static int FindKeyInsertIndex(IList<IniLine> lines, int sectionIndex)
        {
            var insert = sectionIndex + 1;
            for (var i = sectionIndex + 1; i < lines.Count; i++)
            {
                var lt = lines[i].Type;
                if (lt == IniLineType.Section)
                    break;
                if (lt == IniLineType.KeyValue)
                    insert = i + 1;
            }
            return insert;
        }

        private static void AppendSectionWithKey(List<IniLine> lines, string section, string key, string value)
        {
            if (lines.Count > 0 && lines[lines.Count - 1].Type != IniLineType.Empty)
                lines.Add(new IniLine { Type = IniLineType.Empty, RawLine = string.Empty });
            lines.Add(new IniLine { Type = IniLineType.Section, Section = section, RawLine = "[" + section + "]" });
            lines.Add(KeyValueLine(section, key, value));
        }

        private static void RemoveSectionIfEmpty(IList<IniLine> lines, string section)
        {
            var sectionIndex = lines
                .Select((line, index) => new { line, index })
                .FirstOrDefault(x => x.line.Type == IniLineType.Section && x.line.Section == section)
                ?.index ?? -1;
            if (sectionIndex < 0)
                return;

            for (var i = sectionIndex + 1; i < lines.Count; i++)
            {
                var lineType = lines[i].Type;
                if (lineType == IniLineType.Section)
                    break;
                if (lineType == IniLineType.KeyValue)
                    return;
            }

            lines.RemoveAt(sectionIndex);
            if (sectionIndex > 0 && lines[sectionIndex - 1].Type == IniLineType.Empty)
                lines.RemoveAt(sectionIndex - 1);
        }
    }
}
