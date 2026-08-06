using System;
using System.Collections.Generic;
using System.Text;

namespace SmartGoldbergEmu.StubKit
{
    /// <summary>
    /// Minimal PE32/PE32+ reader/writer sufficient for SteamStub 3.1 removal.
    /// </summary>
    internal sealed class PeImage
    {
        public byte[] Data { get; private set; }
        public int PeOffset { get; private set; }
        public ushort NumberOfSections { get; private set; }
        public ushort SizeOfOptionalHeader { get; private set; }
        public ushort Magic { get; private set; }
        public uint AddressOfEntryPoint { get; set; }
        public ulong ImageBase { get; private set; }
        public uint SizeOfImage { get; set; }
        public uint CheckSum { get; set; }
        public List<Section> Sections { get; private set; }
        public List<ulong> TlsCallbacks { get; private set; }

        public bool IsPe32
        {
            get { return Magic == 0x10B; }
        }

        public bool IsPe32Plus
        {
            get { return Magic == 0x20B; }
        }

        public sealed class Section
        {
            public string Name;
            public uint VirtualSize;
            public uint VirtualAddress;
            public uint SizeOfRawData;
            public uint PointerToRawData;
            public uint Characteristics;
            public int HeaderOffset;
        }

        public static PeImage Load(byte[] data)
        {
            if (data == null || data.Length < 0x40)
                throw new InvalidOperationException("File too small.");
            if (data[0] != (byte)'M' || data[1] != (byte)'Z')
                throw new InvalidOperationException("Not an MZ executable.");

            var pe = new PeImage { Data = data };
            pe.PeOffset = BitConverter.ToInt32(data, 0x3C);
            if (pe.PeOffset <= 0 || pe.PeOffset + 0x18 >= data.Length)
                throw new InvalidOperationException("Invalid PE offset.");
            if (BitConverter.ToUInt32(data, pe.PeOffset) != 0x00004550)
                throw new InvalidOperationException("Missing PE signature.");

            pe.NumberOfSections = BitConverter.ToUInt16(data, pe.PeOffset + 6);
            pe.SizeOfOptionalHeader = BitConverter.ToUInt16(data, pe.PeOffset + 20);

            int opt = pe.PeOffset + 24;
            pe.Magic = BitConverter.ToUInt16(data, opt);
            if (pe.Magic != 0x10B && pe.Magic != 0x20B)
                throw new InvalidOperationException("Unsupported optional header magic.");

            pe.AddressOfEntryPoint = BitConverter.ToUInt32(data, opt + 16);
            if (pe.IsPe32Plus)
                pe.ImageBase = BitConverter.ToUInt64(data, opt + 24);
            else
                pe.ImageBase = BitConverter.ToUInt32(data, opt + 28);

            // SizeOfImage / CheckSum sit at the same offsets for PE32 and PE32+.
            pe.SizeOfImage = BitConverter.ToUInt32(data, opt + 56);
            pe.CheckSum = BitConverter.ToUInt32(data, opt + 64);

            pe.Sections = new List<Section>(pe.NumberOfSections);
            int secOff = opt + pe.SizeOfOptionalHeader;
            for (int i = 0; i < pe.NumberOfSections; i++)
            {
                int o = secOff + i * 40;
                var nameBytes = new byte[8];
                Buffer.BlockCopy(data, o, nameBytes, 0, 8);
                string name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

                pe.Sections.Add(new Section
                {
                    Name = name,
                    VirtualSize = BitConverter.ToUInt32(data, o + 8),
                    VirtualAddress = BitConverter.ToUInt32(data, o + 12),
                    SizeOfRawData = BitConverter.ToUInt32(data, o + 16),
                    PointerToRawData = BitConverter.ToUInt32(data, o + 20),
                    Characteristics = BitConverter.ToUInt32(data, o + 36),
                    HeaderOffset = o
                });
            }

            pe.TlsCallbacks = pe.ReadTlsCallbacks();
            return pe;
        }

        private List<ulong> ReadTlsCallbacks()
        {
            var list = new List<ulong>();
            try
            {
                int opt = PeOffset + 24;
                // DataDirectory[9] = TLS
                int dd = IsPe32Plus ? opt + 112 + 9 * 8 : opt + 96 + 9 * 8;
                uint tlsRva = BitConverter.ToUInt32(Data, dd);
                if (tlsRva == 0)
                    return list;

                uint tlsOff = RvaToOffset(tlsRva);
                ulong callbacksVa;
                if (IsPe32Plus)
                    callbacksVa = BitConverter.ToUInt64(Data, (int)tlsOff + 24);
                else
                    callbacksVa = BitConverter.ToUInt32(Data, (int)tlsOff + 12);

                if (callbacksVa == 0)
                    return list;

                uint cbRva = (uint)(callbacksVa - ImageBase);
                uint cbOff = RvaToOffset(cbRva);
                for (int i = 0; i < 16; i++)
                {
                    ulong cb;
                    if (IsPe32Plus)
                    {
                        cb = BitConverter.ToUInt64(Data, (int)cbOff + i * 8);
                        if (cb == 0)
                            break;
                    }
                    else
                    {
                        cb = BitConverter.ToUInt32(Data, (int)cbOff + i * 4);
                        if (cb == 0)
                            break;
                    }
                    list.Add(cb);
                }
            }
            catch
            {
                // TLS is optional for detection/removal paths that do not need it.
            }
            return list;
        }

        public uint VaToRva(ulong va)
        {
            if (va < ImageBase)
                throw new InvalidOperationException("VA below image base.");
            return (uint)(va - ImageBase);
        }

        public Section FindSection(string name)
        {
            for (int i = 0; i < Sections.Count; i++)
            {
                if (string.Equals(Sections[i].Name, name, StringComparison.Ordinal))
                    return Sections[i];
            }
            return null;
        }

        public Section SectionFromRva(uint rva)
        {
            for (int i = 0; i < Sections.Count; i++)
            {
                var s = Sections[i];
                uint span = Math.Max(s.VirtualSize, s.SizeOfRawData);
                if (rva >= s.VirtualAddress && rva < s.VirtualAddress + span)
                    return s;
            }
            return null;
        }

        public uint RvaToOffset(uint rva)
        {
            var s = SectionFromRva(rva);
            if (s == null)
                throw new InvalidOperationException("RVA not in any section: 0x" + rva.ToString("X"));
            return rva - s.VirtualAddress + s.PointerToRawData;
        }

        public void WriteHeaders()
        {
            int opt = PeOffset + 24;
            WriteU16(PeOffset + 6, (ushort)Sections.Count);
            WriteU32(opt + 16, AddressOfEntryPoint);
            WriteU32(opt + 56, SizeOfImage);
            WriteU32(opt + 64, CheckSum);

            for (int i = 0; i < Sections.Count; i++)
            {
                var s = Sections[i];
                int o = s.HeaderOffset;
                var name = Encoding.ASCII.GetBytes(s.Name);
                for (int n = 0; n < 8; n++)
                    Data[o + n] = n < name.Length ? name[n] : (byte)0;
                WriteU32(o + 8, s.VirtualSize);
                WriteU32(o + 12, s.VirtualAddress);
                WriteU32(o + 16, s.SizeOfRawData);
                WriteU32(o + 20, s.PointerToRawData);
                WriteU32(o + 36, s.Characteristics);
            }

            int secTable = opt + SizeOfOptionalHeader;
            int maxHeaders = NumberOfSections;
            for (int i = Sections.Count; i < maxHeaders; i++)
            {
                int o = secTable + i * 40;
                for (int b = 0; b < 40; b++)
                    Data[o + b] = 0;
            }
        }

        public void WriteU16(int offset, ushort value)
        {
            Data[offset] = (byte)value;
            Data[offset + 1] = (byte)(value >> 8);
        }

        public void WriteU32(int offset, uint value)
        {
            Data[offset] = (byte)value;
            Data[offset + 1] = (byte)(value >> 8);
            Data[offset + 2] = (byte)(value >> 16);
            Data[offset + 3] = (byte)(value >> 24);
        }

        public void WriteU64(int offset, ulong value)
        {
            WriteU32(offset, (uint)value);
            WriteU32(offset + 4, (uint)(value >> 32));
        }

        /// <summary>
        /// Replace the first TLS callback VA (used when SteamStub hijacked TLS).
        /// </summary>
        public void SetFirstTlsCallback(ulong callbackVa)
        {
            var list = new List<ulong>(TlsCallbacks);
            if (list.Count == 0)
                list.Add(callbackVa);
            else
                list[0] = callbackVa;
            WriteTlsCallbacks(list);
        }

        /// <summary>
        /// Rewrite the TLS callback VA array (null-terminated).
        /// </summary>
        public void WriteTlsCallbacks(List<ulong> callbacks)
        {
            if (callbacks == null)
                throw new ArgumentNullException("callbacks");

            int opt = PeOffset + 24;
            int dd = IsPe32Plus ? opt + 112 + 9 * 8 : opt + 96 + 9 * 8;
            uint tlsRva = BitConverter.ToUInt32(Data, dd);
            if (tlsRva == 0)
                throw new InvalidOperationException("No TLS directory.");

            uint tlsOff = RvaToOffset(tlsRva);
            ulong callbacksVa = IsPe32Plus
                ? BitConverter.ToUInt64(Data, (int)tlsOff + 24)
                : BitConverter.ToUInt32(Data, (int)tlsOff + 12);
            if (callbacksVa == 0)
                throw new InvalidOperationException("No TLS callbacks.");

            uint cbOff = RvaToOffset((uint)(callbacksVa - ImageBase));
            int stride = IsPe32Plus ? 8 : 4;

            for (int i = 0; i < callbacks.Count; i++)
            {
                if (IsPe32Plus)
                    WriteU64((int)cbOff + i * stride, callbacks[i]);
                else
                    WriteU32((int)cbOff + i * stride, (uint)callbacks[i]);
            }

            if (IsPe32Plus)
                WriteU64((int)cbOff + callbacks.Count * stride, 0);
            else
                WriteU32((int)cbOff + callbacks.Count * stride, 0);

            TlsCallbacks = new List<ulong>(callbacks);
        }

        /// <summary>
        /// Zero IMAGE_DIRECTORY_ENTRY_SECURITY so leftover Authenticode metadata is gone.
        /// </summary>
        public void ClearSecurityDirectory()
        {
            SetDataDirectory(4, 0, 0);
        }

        public void GetDataDirectory(int index, out uint rva, out uint size)
        {
            int opt = PeOffset + 24;
            int dd = (IsPe32Plus ? opt + 112 : opt + 96) + index * 8;
            rva = BitConverter.ToUInt32(Data, dd);
            size = BitConverter.ToUInt32(Data, dd + 4);
        }

        public void SetDataDirectory(int index, uint rva, uint size)
        {
            int opt = PeOffset + 24;
            int dd = (IsPe32Plus ? opt + 112 : opt + 96) + index * 8;
            WriteU32(dd, rva);
            WriteU32(dd + 4, size);
        }

        public bool SectionContainsRva(Section section, uint rva)
        {
            if (section == null || rva == 0)
                return false;
            uint start = section.VirtualAddress;
            uint end = start + Math.Max(section.VirtualSize, section.SizeOfRawData);
            return rva >= start && rva < end;
        }

        /// <summary>
        /// Some SteamStub 2.x builds place IMAGE_IMPORT_DESCRIPTOR in .bind while ILT/IAT
        /// stay in .rdata (e.g. The Cursed Crusade). Copy surviving descriptors into
        /// section raw slack and retarget the Import data directory so .bind can be dropped.
        /// </summary>
        /// <returns>true if import dir was moved (or did not need moving).</returns>
        public bool TryRelocateImportDirectoryOutOfSection(Section fromSection)
        {
            if (fromSection == null)
                return true;

            uint impRva, impSize;
            GetDataDirectory(1, out impRva, out impSize);
            if (impRva == 0 || !SectionContainsRva(fromSection, impRva))
                return true;

            uint impOff = RvaToOffset(impRva);
            var keep = new List<byte[]>();
            for (int i = 0; i < 256; i++)
            {
                int o = (int)impOff + i * 20;
                if (o + 20 > Data.Length)
                    break;

                uint ilt = BitConverter.ToUInt32(Data, o);
                uint nameRva = BitConverter.ToUInt32(Data, o + 12);
                uint iat = BitConverter.ToUInt32(Data, o + 16);
                if (ilt == 0 && nameRva == 0 && iat == 0)
                    break;

                // Drop descriptors whose name lives inside .bind (stub-only imports).
                if (SectionContainsRva(fromSection, nameRva))
                    continue;

                var desc = new byte[20];
                Buffer.BlockCopy(Data, o, desc, 0, 20);
                keep.Add(desc);
            }

            if (keep.Count == 0)
                return false;

            int bytesNeeded = (keep.Count + 1) * 20; // + null terminator
            Section dest;
            uint destRva;
            int destOff;
            if (!TryFindSectionSlack(bytesNeeded, fromSection, out dest, out destRva, out destOff))
                return false;

            for (int i = 0; i < keep.Count; i++)
                Buffer.BlockCopy(keep[i], 0, Data, destOff + i * 20, 20);
            for (int b = 0; b < 20; b++)
                Data[destOff + keep.Count * 20 + b] = 0;

            SetDataDirectory(1, destRva, (uint)bytesNeeded);
            return true;
        }

        private bool TryFindSectionSlack(int bytesNeeded, Section exclude, out Section dest, out uint destRva, out int destOff)
        {
            dest = null;
            destRva = 0;
            destOff = 0;

            foreach (var pref in new[] { ".rdata", ".rsrc", ".data", ".reloc" })
            {
                var s = FindSection(pref);
                if (s == null || s == exclude)
                    continue;
                if (TrySlackInSection(s, bytesNeeded, out destRva, out destOff))
                {
                    dest = s;
                    return true;
                }
            }

            for (int i = 0; i < Sections.Count; i++)
            {
                var s = Sections[i];
                if (s == exclude || s.Name == ".bind")
                    continue;
                if (TrySlackInSection(s, bytesNeeded, out destRva, out destOff))
                {
                    dest = s;
                    return true;
                }
            }

            return false;
        }

        private bool TrySlackInSection(Section s, int bytesNeeded, out uint destRva, out int destOff)
        {
            destRva = 0;
            destOff = 0;
            if (s.SizeOfRawData <= s.VirtualSize)
                return false;

            uint slackStart = s.VirtualSize;
            uint aligned = (slackStart + 3u) & ~3u;
            if (aligned >= s.SizeOfRawData)
                return false;
            uint slack = s.SizeOfRawData - aligned;
            if (slack < (uint)bytesNeeded)
                return false;

            destRva = s.VirtualAddress + aligned;
            destOff = (int)(s.PointerToRawData + aligned);
            if (destOff + bytesNeeded > Data.Length)
                return false;
            return true;
        }

        /// <summary>
        /// Drop TLS callbacks whose RVA falls inside <paramref name="section"/> and compact the list.
        /// Needed when the stub installs itself as a TLS callback inside .bind.
        /// </summary>
        public void RemoveTlsCallbacksInSection(Section section)
        {
            if (section == null || TlsCallbacks == null || TlsCallbacks.Count == 0)
                return;

            uint start = section.VirtualAddress;
            uint end = start + Math.Max(section.VirtualSize, section.SizeOfRawData);

            var keep = new List<ulong>();
            foreach (ulong cb in TlsCallbacks)
            {
                uint rva = (uint)(cb - ImageBase);
                if (rva < start || rva >= end)
                    keep.Add(cb);
            }

            if (keep.Count == TlsCallbacks.Count)
                return;

            int opt = PeOffset + 24;
            int dd = IsPe32Plus ? opt + 112 + 9 * 8 : opt + 96 + 9 * 8;
            uint tlsRva = BitConverter.ToUInt32(Data, dd);
            if (tlsRva == 0)
                return;

            uint tlsOff = RvaToOffset(tlsRva);
            ulong callbacksVa = IsPe32Plus
                ? BitConverter.ToUInt64(Data, (int)tlsOff + 24)
                : BitConverter.ToUInt32(Data, (int)tlsOff + 12);
            if (callbacksVa == 0)
                return;

            uint cbOff = RvaToOffset((uint)(callbacksVa - ImageBase));
            int stride = IsPe32Plus ? 8 : 4;

            for (int i = 0; i < keep.Count; i++)
            {
                if (IsPe32Plus)
                    WriteU64((int)cbOff + i * stride, keep[i]);
                else
                    WriteU32((int)cbOff + i * stride, (uint)keep[i]);
            }

            // Null-terminate (and wipe the old trailing stub entry).
            if (IsPe32Plus)
                WriteU64((int)cbOff + keep.Count * stride, 0);
            else
                WriteU32((int)cbOff + keep.Count * stride, 0);

            TlsCallbacks = keep;
        }

        /// <summary>
        /// Remove .bind. Prefer truncate when it is last; otherwise drop the header entry only.
        /// </summary>
        public void RemoveBindSection(Section section)
        {
            if (section == null || section.Name != ".bind")
                throw new InvalidOperationException("Expected .bind section.");

            int index = Sections.IndexOf(section);
            if (index < 0)
                throw new InvalidOperationException(".bind not in section list.");

            // Before truncating file data, scrub TLS entries that pointed into .bind.
            RemoveTlsCallbacksInSection(section);

            bool isLast = index == Sections.Count - 1;
            Sections.RemoveAt(index);

            if (isLast)
            {
                uint newSize = section.PointerToRawData;
                var trimmed = new byte[newSize];
                Buffer.BlockCopy(Data, 0, trimmed, 0, (int)newSize);
                Data = trimmed;
                SizeOfImage = section.VirtualAddress;
            }
            else
            {
                // Rare: keep raw bytes but shrink image to previous section end.
                var last = Sections[Sections.Count - 1];
                uint end = last.VirtualAddress + Math.Max(last.VirtualSize, last.SizeOfRawData);
                SizeOfImage = AlignUp(end, GetSectionAlignment());
            }

            CheckSum = 0;
            // Recompute header offsets for remaining sections (table stays contiguous at start).
            int opt = PeOffset + 24;
            int secTable = opt + SizeOfOptionalHeader;
            for (int i = 0; i < Sections.Count; i++)
                Sections[i].HeaderOffset = secTable + i * 40;
        }

        public uint GetSectionAlignment()
        {
            int opt = PeOffset + 24;
            return BitConverter.ToUInt32(Data, opt + 32);
        }

        private static uint AlignUp(uint value, uint align)
        {
            if (align == 0)
                return value;
            uint rem = value % align;
            return rem == 0 ? value : value + (align - rem);
        }
    }
}
