using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public class BuffInfo
    {
        public ushort IconId { get; set; }
        public uint Timer { get; set; }
    }

    public class PlayerBuffs
    {
        private const uint MaxBuffsSafety = 64;

        private readonly MemoryProcess _mem;
        private readonly PlayerCore _player;

        public PlayerBuffs(MemoryProcess mem, PlayerCore player)
        {
            _mem = mem;
            _player = player;
        }

        public uint Count
        {
            get
            {
                uint pBase = _player.Base;
                if (pBase == 0u) return 0u;

                return _mem.ReadUInt(pBase + _mem.GetNodeOffset("buff_count"));
            }
        }

        public List<BuffInfo> GetActiveBuffs()
        {
            var list = new List<BuffInfo>();
            uint pBase = _player.Base;
            if (pBase == 0u) return list;

            uint count = Count;
            if (count == 0u || count > MaxBuffsSafety) return list;

            uint arrayPtr = _mem.ReadUInt(pBase + _mem.GetNodeOffset("buff_array"));
            if (arrayPtr == 0u) return list;

            var buffItemNode = _mem.GetNode("buff_icon_id-index-mul-12") ?? _mem.GetNode("buff_icon_id");
            if (buffItemNode == null) return list;

            uint timerOff = _mem.GetNodeOffset("buff_timer");

            for (uint i = 0u; i < count; i++)
            {
                uint itemAddr = arrayPtr + buffItemNode.EvaluateFormula(i);
                list.Add(new BuffInfo
                {
                    IconId = _mem.ReadUShort(itemAddr),
                    Timer = timerOff > 0u ? _mem.ReadUInt(itemAddr + timerOff) : 0u
                });
            }

            return list;
        }

        public bool HasBuff(ushort iconId) => GetActiveBuffs().Any(b => b.IconId == iconId);
    }
}