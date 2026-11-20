using System.Collections.Generic;

namespace Full_Bot
{
    public class PetSlotInfo
    {
        public int Slot { get; set; }
        public string Name { get; set; }
        public uint Type { get; set; }
        public uint Level { get; set; }
    }

    public class PlayerPets
    {
        private const int MaxPetSlots = 10;

        private readonly MemoryProcess _mem;
        private readonly PlayerCore _player;

        public PlayerPets(MemoryProcess mem, PlayerCore player)
        {
            _mem = mem;
            _player = player;
        }

        public bool IsPetSummoned
        {
            get
            {
                uint pBase = _player.Base;
                if (pBase == 0) return false;

                uint petBaseOff = _mem.GetFirstLocalOffset("pet_base", 0x14E8);
                uint petBase = _mem.ReadUInt(pBase + petBaseOff);
                if (petBase == 0) return false;

                uint summonedOff = _mem.GetFirstLocalOffset("pet_summoned", 0x08);
                return _mem.ReadUInt(petBase + summonedOff) != 0xFFFFFFFF;
            }
        }

        public List<PetSlotInfo> GetPetSlots()
        {
            var list = new List<PetSlotInfo>();
            uint pBase = _player.Base;
            if (pBase == 0) return list;

            uint petBaseOff = _mem.GetFirstLocalOffset("pet_base", 0x14E8);
            uint petBase = _mem.ReadUInt(pBase + petBaseOff);
            if (petBase == 0) return list;

            var slotNode = _mem.GetNode("pet_slot_ptr-index-mul-10-add-10");
            uint arrayBaseOff = _mem.GetFirstLocalOffset("pet_slot_ptr-index-mul-10-add-10", 0x10);

            var nameNode = _mem.GetNode("pet_name");
            var nameOffs = nameNode?.GetLocalOffsetsUInt() ?? new uint[] { 0x34, 0x00 };
            uint typeOff = _mem.GetFirstLocalOffset("pet_type", 0x18);
            uint lvlOff = _mem.GetFirstLocalOffset("pet_level", 0x24);

            for (int i = 0; i < MaxPetSlots; i++)
            {
                uint delta = slotNode != null ? slotNode.EvaluateFormula((uint)i) : (uint)(i * 4);
                uint slotPtr = _mem.ReadUInt(petBase + arrayBaseOff + delta);
                if (slotPtr == 0) continue;

                uint namePtr = _mem.ReadUInt(slotPtr + nameOffs[0]);
                for (int n = 1; n < nameOffs.Length && namePtr != 0; n++)
                    namePtr = _mem.ReadUInt(namePtr + nameOffs[n]);

                list.Add(new PetSlotInfo
                {
                    Slot = i,
                    Name = _mem.ReadUnicodeString(namePtr, 32),
                    Type = _mem.ReadUInt(slotPtr + typeOff),
                    Level = _mem.ReadUInt(slotPtr + lvlOff)
                });
            }
            return list;
        }
    }
}