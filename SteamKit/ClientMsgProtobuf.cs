using System;
using System.Collections.Generic;
using System.IO;

namespace SteamKit
{
    public class ClientMsgProtobuf
    {
        public ProtoHeader ProtoHeader { get; }
        public MessageBody Body { get; }
        private readonly EMsg _eMsg;

        public ClientMsgProtobuf(EMsg eMsg)
        {
            _eMsg = eMsg;
            ProtoHeader = new ProtoHeader();
            Body = new MessageBody();
        }

        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((uint)_eMsg | 0x80000000);
                var headerBytes = SerializeHeader();
                bw.Write(headerBytes.Length);
                bw.Write(headerBytes);
                bw.Write(SerializeBody());
                return ms.ToArray();
            }
        }

        private byte[] SerializeHeader()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                if (ProtoHeader.steamid != 0)
                {
                    WriteFieldKey(bw, 1, 1);
                    bw.Write(ProtoHeader.steamid);
                }
                if (ProtoHeader.client_sessionid != 0)
                {
                    WriteFieldKey(bw, 2, 0);
                    WriteVarInt(bw, ProtoHeader.client_sessionid);
                }
                if (ProtoHeader.JobIDSource != ulong.MaxValue)
                {
                    WriteFieldKey(bw, 10, 1);
                    bw.Write(ProtoHeader.JobIDSource);
                }
                return ms.ToArray();
            }
        }

        private byte[] SerializeBody()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                if (_eMsg == EMsg.ClientLogon)
                {
                    WriteFieldKey(bw, 1, 0);
                    WriteVarInt(bw, Body.protocol_version);
                    WriteFieldKey(bw, 3, 0);
                    WriteVarInt(bw, Body.cell_id);
                    if (!string.IsNullOrEmpty(Body.client_language))
                    {
                        WriteFieldKey(bw, 6, 2);
                        WriteLengthDelimitedString(bw, Body.client_language);
                    }
                    WriteFieldKey(bw, 7, 0);
                    WriteVarInt(bw, Body.client_os_type);
                    if (Body.machine_id != 0)
                    {
                        WriteFieldKey(bw, 30, 2);
                        WriteLengthDelimitedBytes(bw, BitConverter.GetBytes(Body.machine_id));
                    }
                }
                else if (_eMsg == EMsg.ClientHello)
                {
                    WriteFieldKey(bw, 1, 0);
                    WriteVarInt(bw, Body.protocol_version);
                }
                else if (_eMsg == EMsg.ClientPICSProductInfoRequest)
                {
                    foreach (var appId in Body.PICSAppIds)
                    {
                        using (var sub = new MemoryStream())
                        using (var subBw = new BinaryWriter(sub))
                        {
                            WriteFieldKey(subBw, 1, 0);
                            WriteVarInt(subBw, appId);
                            WriteFieldKey(bw, 2, 2);
                            WriteLengthDelimitedBytes(bw, sub.ToArray());
                        }
                    }
                    foreach (var pkgId in Body.PICSPackageIds)
                    {
                        using (var sub = new MemoryStream())
                        using (var subBw = new BinaryWriter(sub))
                        {
                            WriteFieldKey(subBw, 1, 0);
                            WriteVarInt(subBw, pkgId);
                            WriteFieldKey(bw, 1, 2);
                            WriteLengthDelimitedBytes(bw, sub.ToArray());
                        }
                    }
                    if (Body.PICSSingleResponse)
                    {
                        WriteFieldKey(bw, 7, 0);
                        WriteVarInt(bw, 1u);
                    }
                }
                return ms.ToArray();
            }
        }

        public static bool TryParseLogOnResponse(byte[] packet, out LogOnResponseData response)
        {
            response = default;
            if (packet.Length < 4)
                return false;

            uint msg = BitConverter.ToUInt32(packet, 0);
            bool isProto = (msg & 0x80000000) != 0;
            var eMsg = (EMsg)(msg & 0x7FFFFFFF);
            if (eMsg != EMsg.ClientLogOnResponse)
                return false;

            if (isProto)
            {
                if (packet.Length < 8)
                    return false;
                int headerLength = BitConverter.ToInt32(packet, 4);
                int bodyOffset = 8 + headerLength;
                if (headerLength < 0 || bodyOffset > packet.Length)
                    return false;
                return TryParseProtoLogOnResponse(packet, bodyOffset, packet.Length - bodyOffset, out response);
            }

            if (packet.Length < 44)
                return false;
            response.EResult = (uint)BitConverter.ToInt32(packet, 20);
            response.ClientSuppliedSteamId = BitConverter.ToUInt64(packet, 32);
            return true;
        }

        private static bool TryParseProtoLogOnResponse(byte[] body, int offset, int length, out LogOnResponseData response)
        {
            response = default;
            int index = offset;
            int end = offset + length;
            while (index < end)
            {
                if (!TryReadVarUInt64(body, ref index, end, out ulong key))
                    return false;
                int fieldNumber = (int)(key >> 3);
                int wireType = (int)(key & 0x07);
                if (fieldNumber == 1 && wireType == 0)
                {
                    if (!TryReadVarUInt64(body, ref index, end, out ulong result))
                        return false;
                    response.EResult = (uint)result;
                }
                else if (fieldNumber == 20 && wireType == 1)
                {
                    if (index + 8 > end)
                        return false;
                    response.ClientSuppliedSteamId = BitConverter.ToUInt64(body, index);
                    index += 8;
                }
                else if (!SkipField(body, ref index, end, wireType))
                    return false;
            }
            return true;
        }

        private static bool SkipField(byte[] body, ref int index, int end, int wireType)
        {
            switch (wireType)
            {
                case 0:
                    return TryReadVarUInt64(body, ref index, end, out _);
                case 1:
                    if (index + 8 > end)
                        return false;
                    index += 8;
                    return true;
                case 2:
                    if (!TryReadVarUInt64(body, ref index, end, out ulong len))
                        return false;
                    if (len > (ulong)(end - index))
                        return false;
                    index += (int)len;
                    return true;
                case 5:
                    if (index + 4 > end)
                        return false;
                    index += 4;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadVarUInt64(byte[] buffer, ref int index, int end, out ulong value)
        {
            value = 0;
            int shift = 0;
            while (index < end && shift < 64)
            {
                byte b = buffer[index++];
                value |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                    return true;
                shift += 7;
            }
            return false;
        }

        private static void WriteFieldKey(BinaryWriter bw, int fieldNumber, int wireType)
        {
            WriteVarInt(bw, (uint)((fieldNumber << 3) | wireType));
        }

        private static void WriteLengthDelimitedString(BinaryWriter bw, string value)
        {
            WriteLengthDelimitedBytes(bw, System.Text.Encoding.UTF8.GetBytes(value));
        }

        private static void WriteLengthDelimitedBytes(BinaryWriter bw, byte[] bytes)
        {
            WriteVarInt(bw, (uint)bytes.Length);
            bw.Write(bytes);
        }

        public static byte[] GetMessageBody(byte[] packet)
        {
            if (packet.Length < 8 || (BitConverter.ToUInt32(packet, 0) & 0x80000000) == 0)
                return Array.Empty<byte>();
            int headerLength = BitConverter.ToInt32(packet, 4);
            int bodyOffset = 8 + headerLength;
            if (headerLength < 0 || bodyOffset > packet.Length)
                return Array.Empty<byte>();
            int bodyLength = packet.Length - bodyOffset;
            var body = new byte[bodyLength];
            Buffer.BlockCopy(packet, bodyOffset, body, 0, bodyLength);
            return body;
        }

        public static int GetSessionIdFromHeader(byte[] packet)
        {
            if (packet.Length < 8 || (BitConverter.ToUInt32(packet, 0) & 0x80000000) == 0)
                return 0;
            int headerLength = BitConverter.ToInt32(packet, 4);
            if (headerLength <= 0 || 8 + headerLength > packet.Length)
                return 0;
            int start = 8;
            int end = 8 + headerLength;
            int index = start;
            while (index < end)
            {
                if (!TryReadVarUInt64(packet, ref index, end, out ulong key))
                    break;
                int field = (int)(key >> 3);
                int wire = (int)(key & 7);
                if (field == 2 && wire == 0)
                {
                    if (TryReadVarUInt64(packet, ref index, end, out ulong v))
                        return (int)(uint)v;
                    break;
                }
                if (!SkipField(packet, ref index, end, wire))
                    break;
            }
            return 0;
        }

        public static ulong GetSteamIdFromHeader(byte[] packet)
        {
            if (packet.Length < 8 || (BitConverter.ToUInt32(packet, 0) & 0x80000000) == 0)
                return 0;
            int headerLength = BitConverter.ToInt32(packet, 4);
            if (headerLength <= 0 || 8 + headerLength > packet.Length)
                return 0;
            int start = 8;
            int end = 8 + headerLength;
            int index = start;
            while (index < end)
            {
                if (!TryReadVarUInt64(packet, ref index, end, out ulong key))
                    break;
                int field = (int)(key >> 3);
                int wire = (int)(key & 7);
                if (field == 1 && wire == 1)
                {
                    if (index + 8 > end)
                        return 0;
                    ulong sid = BitConverter.ToUInt64(packet, index);
                    index += 8;
                    return sid;
                }
                if (!SkipField(packet, ref index, end, wire))
                    break;
            }
            return 0;
        }

        public static PICSProductInfoResult ParsePICSProductInfoResponse(byte[] body)
        {
            var result = new PICSProductInfoResult();
            int index = 0;
            while (index < body.Length)
            {
                if (!TryReadVarUInt64(body, ref index, body.Length, out ulong key))
                    break;
                int field = (int)(key >> 3);
                int wire = (int)(key & 7);
                if (field == 1 && wire == 2)
                {
                    if (!TryReadVarUInt64(body, ref index, body.Length, out ulong len))
                        break;
                    if (len > (ulong)(body.Length - index))
                        break;
                    result.Apps.Add(ParsePICSItem(body, index, (int)len, isPackage: false));
                    index += (int)len;
                }
                else if (field == 2 && wire == 0)
                {
                    if (!TryReadVarUInt64(body, ref index, body.Length, out ulong value))
                        break;
                    result.UnknownAppIds.Add((uint)value);
                }
                else if (field == 3 && wire == 2)
                {
                    if (!TryReadVarUInt64(body, ref index, body.Length, out ulong len))
                        break;
                    if (len > (ulong)(body.Length - index))
                        break;
                    result.Packages.Add(ParsePICSItem(body, index, (int)len, isPackage: true));
                    index += (int)len;
                }
                else if (field == 4 && wire == 0)
                {
                    if (!TryReadVarUInt64(body, ref index, body.Length, out ulong value))
                        break;
                    result.UnknownPackageIds.Add((uint)value);
                }
                else if (field == 6 && wire == 0)
                {
                    if (!TryReadVarUInt64(body, ref index, body.Length, out ulong v))
                        break;
                    result.ResponsePending = v != 0;
                }
                else if (!SkipField(body, ref index, body.Length, wire))
                    break;
            }
            return result;
        }

        private static PICSProductInfoItem ParsePICSItem(byte[] body, int offset, int length, bool isPackage)
        {
            var item = new PICSProductInfoItem { IsPackage = isPackage };
            int index = offset;
            int end = offset + length;
            while (index < end)
            {
                if (!TryReadVarUInt64(body, ref index, end, out ulong key))
                    break;
                int field = (int)(key >> 3);
                int wire = (int)(key & 7);
                if (field == 1 && wire == 0)
                {
                    if (TryReadVarUInt64(body, ref index, end, out ulong v))
                        item.ID = (uint)v;
                }
                else if (field == 5 && wire == 2)
                {
                    if (!TryReadVarUInt64(body, ref index, end, out ulong len))
                        break;
                    if (len > (ulong)(end - index))
                        break;
                    item.Buffer = new byte[(int)len];
                    Buffer.BlockCopy(body, index, item.Buffer, 0, (int)len);
                    index += (int)len;
                }
                else if (!SkipField(body, ref index, end, wire))
                    break;
            }
            return item;
        }

        private static void WriteVarInt(BinaryWriter bw, uint value)
        {
            while (value >= 0x80)
            {
                bw.Write((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            bw.Write((byte)(value & 0x7F));
        }

        private static void WriteVarInt(BinaryWriter bw, ulong value)
        {
            while (value >= 0x80)
            {
                bw.Write((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            bw.Write((byte)(value & 0x7F));
        }
    }

    public struct LogOnResponseData
    {
        public uint EResult { get; set; }
        public ulong ClientSuppliedSteamId { get; set; }
    }

    public class ProtoHeader
    {
        public uint client_sessionid { get; set; }
        public ulong steamid { get; set; }
        public ulong JobIDSource { get; set; } = ulong.MaxValue;
    }

    public class MessageBody
    {
        public uint protocol_version { get; set; }
        public uint client_os_type { get; set; }
        public string client_language { get; set; } = "english";
        public uint cell_id { get; set; }
        public ulong machine_id { get; set; }
        public List<uint> PICSAppIds { get; } = new List<uint>();
        public List<uint> PICSPackageIds { get; } = new List<uint>();
        public bool PICSSingleResponse { get; set; }
    }
}
