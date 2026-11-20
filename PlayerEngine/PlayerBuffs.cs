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
                if (pBase == 0) return 0;
                uint off = _mem.GetFirstLocalOffset("buff_count", 0x398);
                return _mem.ReadUInt(pBase + off);
            }
        }

        public List<BuffInfo> GetActiveBuffs()
        {
            var list = new List<BuffInfo>();
            uint pBase = _player.Base;
            if (pBase == 0) return list;

            uint count = Count;
            if (count == 0 || count > MaxBuffsSafety) return list;

            uint buffArrayOff = _mem.GetFirstLocalOffset("buff_array", 0x390);
            uint arrayPtr = _mem.ReadUInt(pBase + buffArrayOff);
            if (arrayPtr == 0) return list;

            var buffItemNode = _mem.GetNode("buff_icon_id-index-mul-12") ?? _mem.GetNode("buff_icon_id");
            uint timerOff = _mem.GetFirstLocalOffset("buff_timer", 0x02);

            for (uint i = 0; i < count; i++)
            {
                uint delta = buffItemNode != null ? buffItemNode.EvaluateFormula(i) : (i * 0x12u);
                uint itemAddr = arrayPtr + delta;

                list.Add(new BuffInfo
                {
                    IconId = _mem.ReadUShort(itemAddr),
                    Timer = _mem.ReadUInt(itemAddr + timerOff)
                });
            }
            return list;
        }

        public bool HasBuff(ushort iconId)
        {
            return GetActiveBuffs().Any(b => b.IconId == iconId);
        }
    }
}