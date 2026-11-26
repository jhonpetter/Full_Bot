using System.Collections.Generic;

namespace Full_Bot
{
    public class PartyMemberInfo
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
    }

    public class PartyCore
    {
        private const int MaxPartyMembers = 10;
        private const uint MaxMemberBuffsSafety = 128;

        private readonly MemoryProcess _mem;
        private readonly PlayerCore _player;

        public PartyCore(MemoryProcess mem, PlayerCore player = null)
        {
            _mem = mem;
            _player = player;
        }

        private uint PartyBase
        {
            get
            {
                uint pBase = (_player != null && _player.Base != 0u)
                    ? _player.Base
                    : _mem.ResolveStructBase("player_struct");

                if (!MemoryProcess.IsValidPointer(pBase)) return 0u;

                uint partyBase = _mem.ReadUIntFromNodeAtBase(pBase, "party_base");
                if (MemoryProcess.IsValidPointer(partyBase))
                    return partyBase;

                uint partyOff = _mem.GetNodeOffset("party_base");
                return partyOff > 0u ? _mem.ReadUInt(pBase + partyOff) : 0u;
            }
        }

        private uint GetNodeOffset(string key)
        {
            var node = _mem.GetNode(key);
            if (node == null) return 0u;

            var local = node.GetLocalOffsetsUInt();
            if (local != null && local.Length > 0)
                return local[0];

            return node.HasOwnAddressDisplacement ? node.OwnAddressDisplacement : 0u;
        }

        public bool IsInParty() => GetMemberCount() > 0u;

        public uint GetMemberCount()
        {
            uint pBase = PartyBase;
            if (!MemoryProcess.IsValidPointer(pBase)) return 0u;

            uint count = _mem.ReadUInt(pBase + _mem.GetNodeOffset("party_member_count"));
            return count <= MaxPartyMembers ? count : 0u;
        }

        private void ReadMemberBuffs(uint memberPtr, PartyMemberInfo info)
        {
            if (!MemoryProcess.IsValidPointer(memberPtr) || info == null) return;

            uint buffCount = _mem.ReadUInt(memberPtr + _mem.GetNodeOffset("party_member_buff_count"));
            uint buffArrayPtr = _mem.ReadUInt(memberPtr + _mem.GetNodeOffset("party_member_buff_array"));

            if (!MemoryProcess.IsValidPointer(buffArrayPtr) || buffCount == 0u || buffCount > MaxMemberBuffsSafety)
                return;

            var buffSlotNode = _mem.GetNode("party_member_buff_icon_id-index-mul-12")
                ?? _mem.GetNode("party_member_buff_icon_id");
            if (buffSlotNode == null) return;

            uint iconExtraOff = GetNodeOffset("party_member_buff_icon_id");

            for (uint b = 0u; b < buffCount; b++)
            {
                uint addr = buffArrayPtr + buffSlotNode.EvaluateFormula(b) + iconExtraOff;
                ushort iconId = _mem.ReadUShort(addr);
                if (iconId == 0) continue;

                if (!info.BuffIconIds.Contains(iconId))
                    info.BuffIconIds.Add(iconId);

                if (!info.BuffIds.Contains(iconId))
                    info.BuffIds.Add(iconId);
            }
        }

        public List<PartyMemberInfo> GetMembers()
        {
            var members = new List<PartyMemberInfo>();
            uint pBase = PartyBase;
            if (!MemoryProcess.IsValidPointer(pBase)) return members;

            uint count = _mem.ReadUInt(pBase + _mem.GetNodeOffset("party_member_count"));
            uint arrayPtr = _mem.ReadUInt(pBase + _mem.GetNodeOffset("party_member_array"));

            if (!MemoryProcess.IsValidPointer(arrayPtr) || count == 0u || count > MaxPartyMembers)
                return members;

            var slotNode = _mem.GetNode("party_member_ptr-index-mul-4")
                ?? _mem.GetNode("party_member_ptr");
            if (slotNode == null) return members;

            uint idOff = _mem.GetNodeOffset("party_member_id");
            uint lvlOff = _mem.GetNodeOffset("party_member_level");
            uint hpOff = _mem.GetNodeOffset("party_member_hp");
            uint mpOff = _mem.GetNodeOffset("party_member_mp");
            uint hpMaxOff = _mem.GetNodeOffset("party_member_hp_max");
            uint mpMaxOff = _mem.GetNodeOffset("party_member_mp_max");
            uint xOff = _mem.GetNodeOffset("party_member_pos_x");
            uint yOff = _mem.GetNodeOffset("party_member_pos_y");
            uint zOff = _mem.GetNodeOffset("party_member_pos_z");

            for (uint i = 0u; i < count; i++)
            {
                uint mPtr = _mem.ReadUInt(arrayPtr + slotNode.EvaluateFormula(i));
                if (!MemoryProcess.IsValidPointer(mPtr)) continue;

                uint memberId = _mem.ReadUInt(mPtr + idOff);
                if (memberId == 0u) continue;

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
                    HPMax = hpMax > 0u ? hpMax : hp,
                    MPMax = mpMax > 0u ? mpMax : mp,
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
            if (id == 0u) return null;
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