using System;
using System.Collections.Generic;

namespace Full_Bot
{
    /*public class PartyMemberInfo
    {
        public int Index { get; set; }
        public uint Id { get; set; }
        public uint Level { get; set; }
        public uint HP { get; set; }
        public uint MP { get; set; }
        public uint HPMax { get; set; }
        public uint MPMax { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public List<ushort> BuffIconIds { get; set; } = new List<ushort>();
        public List<uint> BuffIds { get; set; } = new List<uint>();

        public float HpPercent => HPMax > 0 ? (HP * 100f / HPMax) : 0f;
        public float MpPercent => MPMax > 0 ? (MP * 100f / MPMax) : 0f;

        public bool HasBuff(uint buffId)
        {
            if (buffId == 0) return false;
            for (int i = 0; i < BuffIds.Count; i++)
            {
                if (BuffIds[i] == buffId)
                    return true;
            }
            return false;
        }
    }*/

    public class PartyCore
    {
        private const int MaxPartyMembers = 10;
        private const uint MaxMemberBuffsSafety = 128;
        private const uint MinValidUserPtr = 0x00010000u;
        private const uint MaxValidUserPtr = 0x7FFFFFFFu;

        private readonly MemoryProcess _mem;

        public PartyCore(MemoryProcess mem)
        {
            _mem = mem;
        }

        private uint PlayerStructBase
        {
            get
            {
                var pNode = _mem.GetNode("player_struct");
                if (pNode != null)
                {
                    var offs = pNode.GetOffsetsUInt();
                    if (offs.Length > 0)
                        return _mem.ResolvePointer(_mem.GetNodeBaseAddress(pNode), offs);
                }

                var baseNode = _mem.GetNode("base_address") ?? _mem.GetNode("base_ptr_raw");
                uint gameRunOff = _mem.GetFirstLocalOffset("struct_base", 0x1C);
                uint playerOff = _mem.GetFirstLocalOffset("player_struct", 0x2C);
                return _mem.ResolvePointer(_mem.GetNodeBaseAddress(baseNode), gameRunOff, playerOff);
            }
        }

        private uint PartyBase
        {
            get
            {
                uint pStruct = PlayerStructBase;
                if (!IsLikelyValidPointer(pStruct)) return 0;

                var partyNode = _mem.GetNode("party_base");
                if (partyNode != null)
                {
                    var localOffs = partyNode.GetLocalOffsetsUInt();
                    if (localOffs.Length > 0)
                        return _mem.ResolvePointer(pStruct, localOffs);
                }

                uint partyOff = _mem.GetFirstLocalOffset("party_base", 0x7B0);
                return _mem.ReadUInt(pStruct + partyOff);
            }
        }

        private static bool IsLikelyValidPointer(uint ptr)
        {
            return ptr >= MinValidUserPtr && ptr <= MaxValidUserPtr;
        }

        public bool IsInParty()
        {
            return GetMemberCount() > 0;
        }

        public uint GetMemberCount()
        {
            uint pBase = PartyBase;
            if (!IsLikelyValidPointer(pBase)) return 0;

            uint countOff = _mem.GetFirstLocalOffset("party_member_count", 0x18);
            uint count = _mem.ReadUInt(pBase + countOff);
            if (count > MaxPartyMembers) return 0;
            return count;
        }

        private void ReadMemberBuffs(uint memberPtr, PartyMemberInfo info)
        {
            if (!IsLikelyValidPointer(memberPtr) || info == null) return;

            uint buffCountOff = _mem.GetFirstLocalOffset("party_member_buff_count", 0x64);
            uint buffArrayOff = _mem.GetFirstLocalOffset("party_member_buff_array", 0x58);

            uint buffCount = _mem.ReadUInt(memberPtr + buffCountOff);
            uint buffArrayPtr = _mem.ReadUInt(memberPtr + buffArrayOff);

            if (!IsLikelyValidPointer(buffArrayPtr) || buffCount == 0 || buffCount > MaxMemberBuffsSafety)
                return;

            var buffSlotNode = _mem.GetNode("party_member_buff_icon_id-index-mul-12");

            for (uint b = 0; b < buffCount; b++)
            {
                uint delta = buffSlotNode != null ? buffSlotNode.EvaluateFormula(b) : (b * 12u);
                ushort iconId = _mem.ReadUShort(buffArrayPtr + delta);
                if (iconId == 0) continue;

                info.BuffIconIds.Add(iconId);
                info.BuffIds.Add(iconId);
            }
        }

        public List<PartyMemberInfo> GetMembers()
        {
            var members = new List<PartyMemberInfo>();
            uint pBase = PartyBase;
            if (!IsLikelyValidPointer(pBase)) return members;

            uint countOff = _mem.GetFirstLocalOffset("party_member_count", 0x18);
            uint arrayOff = _mem.GetFirstLocalOffset("party_member_array", 0x14);

            uint count = _mem.ReadUInt(pBase + countOff);
            uint arrayPtr = _mem.ReadUInt(pBase + arrayOff);

            if (!IsLikelyValidPointer(arrayPtr) || count == 0 || count > MaxPartyMembers)
                return members;

            var slotNode = _mem.GetNode("party_member_ptr-index-mul-4");
            uint slotLocalOff = 0;
            if (slotNode != null)
            {
                var sOffs = slotNode.GetLocalOffsetsUInt();
                if (sOffs.Length > 0)
                    slotLocalOff = sOffs[0];
            }
            else
            {
                slotLocalOff = 0x4;
            }

            uint idOff = _mem.GetFirstLocalOffset("party_member_id", 0xC);
            uint lvlOff = _mem.GetFirstLocalOffset("party_member_level", 0x10);
            uint hpOff = _mem.GetFirstLocalOffset("party_member_hp", 0x1C);
            uint mpOff = _mem.GetFirstLocalOffset("party_member_mp", 0x20);
            uint hpMaxOff = _mem.GetFirstLocalOffset("party_member_hp_max", 0x24);
            uint mpMaxOff = _mem.GetFirstLocalOffset("party_member_mp_max", 0x28);
            uint xOff = _mem.GetFirstLocalOffset("party_member_pos_x", 0x34);
            uint yOff = _mem.GetFirstLocalOffset("party_member_pos_y", 0x38);
            uint zOff = _mem.GetFirstLocalOffset("party_member_pos_z", 0x3C);

            for (uint i = 0; i < count; i++)
            {
                uint delta = slotNode != null ? slotNode.EvaluateFormula(i) : (i * 4u);
                uint mPtr = _mem.ReadUInt(arrayPtr + slotLocalOff + delta);
                if (!IsLikelyValidPointer(mPtr)) continue;

                uint memberId = _mem.ReadUInt(mPtr + idOff);
                if (memberId == 0) continue;

                uint hp = _mem.ReadUInt(mPtr + hpOff);
                uint mp = _mem.ReadUInt(mPtr + mpOff);
                uint hpMax = _mem.ReadUInt(mPtr + hpMaxOff);
                uint mpMax = _mem.ReadUInt(mPtr + mpMaxOff);

                var info = new PartyMemberInfo
                {
                    Index = (int)i,
                    Id = memberId,
                    Level = _mem.ReadUInt(mPtr + lvlOff),
                    HP = hp,
                    MP = mp,
                    HPMax = hpMax > 0 ? hpMax : hp,
                    MPMax = mpMax > 0 ? mpMax : mp,
                    X = _mem.ReadFloat(mPtr + xOff),
                    Y = _mem.ReadFloat(mPtr + yOff),
                    Z = _mem.ReadFloat(mPtr + zOff)
                };

                ReadMemberBuffs(mPtr, info);
                members.Add(info);
            }

            return members;
        }

        public PartyMemberInfo GetMemberById(uint id)
        {
            if (id == 0) return null;
            foreach (var m in GetMembers())
            {
                if (m.Id == id) return m;
            }
            return null;
        }

        public PartyMemberInfo GetLeader()
        {
            var members = GetMembers();
            return members.Count > 0 ? members[0] : null;
        }
    }
}