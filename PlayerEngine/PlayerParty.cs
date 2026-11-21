using System.Collections.Generic;

namespace Full_Bot
{
    /*public class PartyMemberInfo
    {
        public uint Id { get; set; }
        public uint Level { get; set; }
        public uint HP { get; set; }
        public uint HPMax { get; set; }
        public uint MP { get; set; }
        public uint MPMax { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }*/
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

    public class PlayerParty
    {
        private const uint MaxPartyMembersSafety = 10;

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;
        private readonly PlayerCore _player;
        private uint _lastAcceptedTimeHit = 0;

        public PlayerParty(MemoryProcess mem, AsmRunner asm, PlayerCore player)
        {
            _mem = mem;
            _asm = asm;
            _player = player;
        }

        public List<PartyMemberInfo> GetMembers()
        {
            var list = new List<PartyMemberInfo>();
            uint pBase = _player.Base;
            if (pBase == 0) return list;

            uint partyBaseOff = _mem.GetFirstLocalOffset("party_base", 0x7B0);
            uint partyBase = _mem.ReadUInt(pBase + partyBaseOff);
            if (partyBase == 0) return list;

            uint countOff = _mem.GetFirstLocalOffset("party_member_count", 0x18);
            uint arrayOff = _mem.GetFirstLocalOffset("party_member_array", 0x14);

            uint count = _mem.ReadUInt(partyBase + countOff);
            uint arrayPtr = _mem.ReadUInt(partyBase + arrayOff);
            if (arrayPtr == 0 || count == 0 || count > MaxPartyMembersSafety) return list;

            var slotNode = _mem.GetNode("party_member_ptr-index-mul-4");

            uint idOff = _mem.GetFirstLocalOffset("party_member_id", 0x0C);
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
                uint mPtr = _mem.ReadUInt(arrayPtr + delta);
                if (mPtr == 0) continue;

                list.Add(new PartyMemberInfo
                {
                    Id = _mem.ReadUInt(mPtr + idOff),
                    Level = _mem.ReadUInt(mPtr + lvlOff),
                    HP = _mem.ReadUInt(mPtr + hpOff),
                    MP = _mem.ReadUInt(mPtr + mpOff),
                    HPMax = _mem.ReadUInt(mPtr + hpMaxOff),
                    MPMax = _mem.ReadUInt(mPtr + mpMaxOff),
                    X = _mem.ReadFloat(mPtr + xOff),
                    Y = _mem.ReadFloat(mPtr + yOff),
                    Z = _mem.ReadFloat(mPtr + zOff)
                });
            }
            return list;
        }

        public void Invite(uint targetPlayerId)
        {
            var vars = new Dictionary<string, uint> { { "TARGET_PLAYER_ID", targetPlayerId } };
            _asm.RunScript(_mem, @"Scripts\InviteParty.asm", vars);
        }

        public bool TryAcceptPendingInvite(List<uint> allowedLeaderIds = null)
        {
            var dialogCountNode = _mem.GetNode("dialog_count");
            uint dialogCountAddr = _mem.GetNodeBaseAddress(dialogCountNode);
            if (dialogCountAddr == 0 || _mem.ReadUInt(dialogCountAddr) == 0) return false;

            var dialogBaseNode = _mem.GetNode("dialog_popup_base");
            uint dialogBasePtrAddr = _mem.GetNodeBaseAddress(dialogBaseNode);
            uint dialogBase = dialogBasePtrAddr != 0 ? _mem.ReadUInt(dialogBasePtrAddr) : 0;
            if (dialogBase == 0) return false;

            uint leaderOff = _mem.GetFirstLocalOffset("dialog_player_id", 0x10);
            uint timeHitOff = _mem.GetFirstLocalOffset("dialog_time_hit", 0x14);

            uint leaderId = _mem.ReadUInt(dialogBase + leaderOff);
            uint timeHit = _mem.ReadUInt(dialogBase + timeHitOff);

            if (leaderId == 0 || timeHit == 0 || timeHit == _lastAcceptedTimeHit)
                return false;

            if (allowedLeaderIds != null && allowedLeaderIds.Count > 0 && !allowedLeaderIds.Contains(leaderId))
                return false;

            _lastAcceptedTimeHit = timeHit;

            var vars = new Dictionary<string, uint>
            {
                { "LEADER_ID", leaderId },
                { "TIME_HIT", timeHit }
            };
            _asm.RunScript(_mem, @"Scripts\AcceptParty.asm", vars);
            return true;
        }
    }
}