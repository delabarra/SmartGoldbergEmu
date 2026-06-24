using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    public class IniFile
    {
        public List<IniLine> Lines { get; set; } = new List<IniLine>();
    }

    public class IniLine
    {
        public IniLineType Type { get; set; }
        public string RawLine { get; set; }
        public string Section { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public enum IniLineType
    {
        Empty,
        Comment,
        Section,
        KeyValue
    }
}
