using System;
using System.Collections.Generic;
using System.Text;

namespace SteamKit
{
    /// <summary>Steam wire message ids needed for anonymous CM logon and PICS product info.</summary>
    public enum EMsg : uint
    {
        Multi = 1,
        ChannelEncryptRequest = 1303,
        ChannelEncryptResponse = 1304,
        ChannelEncryptResult = 1305,
        ClientLogOnResponse = 751,
        ClientHello = 9805,
        ClientLogon = 5514,
        ClientPICSProductInfoRequest = 8903,
        ClientPICSProductInfoResponse = 8904,
    }

    public enum EUniverse : uint
    {
        Invalid = 0,
        Public = 1,
    }

    /// <summary>Valve KeyValues tree for PICS app (text) and package (binary) payloads.</summary>
    public class KeyValue
    {
        // Real Steam PICS trees are shallow; cap recursion so hostile input cannot blow the stack.
        private const int MaxParseDepth = 64;

        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public List<KeyValue> Children { get; } = new List<KeyValue>();

        public KeyValue() { }
        public KeyValue(string name, string value = "") { Name = name; Value = value; }

        public static KeyValue ParseVdf(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return null;
            int length = buffer.Length;
            while (length > 0 && buffer[length - 1] == 0) length--;
            string text = Encoding.UTF8.GetString(buffer, 0, length);
            if (text.Length > 0 && text[0] == '\uFEFF')
                text = text.Substring(1);
            var tokens = TokenizeVdf(text);
            int pos = 0;
            return pos < tokens.Count ? ParseVdfNode(tokens, ref pos, depth: 0) : null;
        }

        private static KeyValue ParseVdfNode(List<string> tokens, ref int pos, int depth)
        {
            if (depth > MaxParseDepth)
                throw new InvalidOperationException("KeyValue VDF nesting exceeds the maximum supported depth.");

            var kv = new KeyValue { Name = tokens[pos++] };
            if (pos < tokens.Count && tokens[pos] == "{")
            {
                pos++;
                while (pos < tokens.Count && tokens[pos] != "}")
                    kv.Children.Add(ParseVdfNode(tokens, ref pos, depth + 1));
                if (pos < tokens.Count) pos++;
            }
            else if (pos < tokens.Count && tokens[pos] != "}")
            {
                kv.Value = tokens[pos++];
            }
            return kv;
        }

        private static List<string> TokenizeVdf(string text)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c <= ' ') { i++; continue; }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n') i++;
                    continue;
                }
                if (c == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < text.Length && text[i] != '"')
                    {
                        if (text[i] == '\\' && i + 1 < text.Length)
                        {
                            i++;
                            switch (text[i]) { case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; default: sb.Append(text[i]); break; }
                            i++;
                        }
                        else sb.Append(text[i++]);
                    }
                    i++;
                    tokens.Add(sb.ToString());
                }
                else if (c == '{') { tokens.Add("{"); i++; }
                else if (c == '}') { tokens.Add("}"); i++; }
                else
                {
                    var sb = new StringBuilder();
                    while (i < text.Length && text[i] > ' ' && text[i] != '{' && text[i] != '}' && text[i] != '"')
                        sb.Append(text[i++]);
                    if (sb.Length > 0) tokens.Add(sb.ToString());
                }
            }
            return tokens;
        }

        public static KeyValue ParseBinaryKV(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 5) return null;
            int pos = 4;
            return ReadBinaryKVNode(buffer, ref pos, depth: 0);
        }

        private static KeyValue ReadBinaryKVNode(byte[] buf, ref int pos, int depth)
        {
            if (depth > MaxParseDepth)
                throw new InvalidOperationException("KeyValue binary nesting exceeds the maximum supported depth.");

            if (pos >= buf.Length) return null;
            byte type = buf[pos++];
            if (type == 0x08) return null;
            string key = ReadNullString(buf, ref pos);
            var kv = new KeyValue { Name = key };
            switch (type)
            {
                case 0x00:
                    while (pos < buf.Length && buf[pos] != 0x08)
                    {
                        var child = ReadBinaryKVNode(buf, ref pos, depth + 1);
                        if (child != null) kv.Children.Add(child);
                    }
                    if (pos < buf.Length) pos++;
                    break;
                case 0x01: kv.Value = ReadNullString(buf, ref pos); break;
                case 0x02: if (pos + 4 <= buf.Length) { kv.Value = BitConverter.ToInt32(buf, pos).ToString(); pos += 4; } break;
                case 0x03: if (pos + 4 <= buf.Length) { kv.Value = BitConverter.ToSingle(buf, pos).ToString(); pos += 4; } break;
                case 0x04:
                case 0x06: if (pos + 4 <= buf.Length) { kv.Value = BitConverter.ToUInt32(buf, pos).ToString(); pos += 4; } break;
                case 0x07: if (pos + 8 <= buf.Length) { kv.Value = BitConverter.ToUInt64(buf, pos).ToString(); pos += 8; } break;
            }
            return kv;
        }

        private static string ReadNullString(byte[] buf, ref int pos)
        {
            int start = pos;
            while (pos < buf.Length && buf[pos] != 0) pos++;
            var s = Encoding.UTF8.GetString(buf, start, pos - start);
            if (pos < buf.Length) pos++;
            return s;
        }
    }

    public class PICSProductInfoResult
    {
        public List<PICSProductInfoItem> Apps { get; } = new List<PICSProductInfoItem>();
        public List<PICSProductInfoItem> Packages { get; } = new List<PICSProductInfoItem>();
        public List<uint> UnknownAppIds { get; } = new List<uint>();
        public List<uint> UnknownPackageIds { get; } = new List<uint>();
        public bool ResponsePending { get; set; }
    }

    public class PICSProductInfoItem
    {
        public uint ID { get; set; }
        public byte[] Buffer { get; set; }
        public bool IsPackage { get; set; }

        public KeyValue ToKeyValue()
        {
            if (Buffer == null || Buffer.Length == 0)
                return null;
            return IsPackage ? KeyValue.ParseBinaryKV(Buffer) : KeyValue.ParseVdf(Buffer);
        }
    }
}
