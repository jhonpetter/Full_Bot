using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public class SkillInfo
    {
        public uint Id { get; set; }
        public uint Level { get; set; }
        public uint Cooldown { get; set; }
        public uint MaxCooldown { get; set; }
        public byte Type { get; set; }
        public bool IsReady => Cooldown == 0;
    }

    public class PlayerSkills
    {
        private const uint MaxSkillsSafety = 250;

        private readonly MemoryProcess _mem;
        private readonly PlayerCore _player;

        public PlayerSkills(MemoryProcess mem, PlayerCore player)
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
                uint off = _mem.GetFirstLocalOffset("skill_count", 0x1510);
                return _mem.ReadUInt(pBase + off);
            }
        }

        public List<SkillInfo> GetLearnedSkills()
        {
            var list = new List<SkillInfo>();
            uint pBase = _player.Base;
            if (pBase == 0) return list;

            uint count = Count;
            if (count == 0 || count > MaxSkillsSafety) return list;

            uint skillArrayOff = _mem.GetFirstLocalOffset("skill_array_base", 0x150C);
            uint arrayPtr = _mem.ReadUInt(pBase + skillArrayOff);
            if (arrayPtr == 0) return list;

            var slotNode = _mem.GetNode("skill_slot_ptr-index-mul-4");

            uint idOff = _mem.GetFirstLocalOffset("skill_id", 0x08);
            uint lvlOff = _mem.GetFirstLocalOffset("skill_level", 0x0C);
            uint cdOff = _mem.GetFirstLocalOffset("skill_cooldown", 0x10);
            uint maxCdOff = _mem.GetFirstLocalOffset("skill_await", 0x14);
            uint typeOff = _mem.GetFirstLocalOffset("skill_type", 0x32);

            for (uint i = 0; i < count; i++)
            {
                uint delta = slotNode != null ? slotNode.EvaluateFormula(i) : (i * 4u);
                uint sPtr = _mem.ReadUInt(arrayPtr + delta);
                if (sPtr == 0) continue;

                list.Add(new SkillInfo
                {
                    Id = _mem.ReadUInt(sPtr + idOff),
                    Level = _mem.ReadUInt(sPtr + lvlOff),
                    Cooldown = _mem.ReadUInt(sPtr + cdOff),
                    MaxCooldown = _mem.ReadUInt(sPtr + maxCdOff),
                    Type = _mem.ReadByte(sPtr + typeOff)
                });
            }
            return list;
        }

        public bool IsSkillReady(uint skillId)
        {
            var skill = GetLearnedSkills().FirstOrDefault(s => s.Id == skillId);
            return skill != null && skill.IsReady;
        }
    }
}