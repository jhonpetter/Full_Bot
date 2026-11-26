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
                if (!MemoryProcess.IsValidPointer(pBase)) return false;

                uint petBase = _mem.ReadUIntFromNodeAtBase(pBase, "pet_base");
                if (!MemoryProcess.IsValidPointer(petBase)) return false;

                return _mem.ReadUIntFromNodeAtBase(petBase, "pet_summoned") != 0xFFFFFFFFu;
            }
        }

        public List<PetSlotInfo> GetPetSlots()
        {
            var list = new List<PetSlotInfo>();
            uint pBase = _player.Base;
            if (!MemoryProcess.IsValidPointer(pBase)) return list;

            uint petBase = _mem.ReadUIntFromNodeAtBase(pBase, "pet_base");
            if (!MemoryProcess.IsValidPointer(petBase)) return list;

            var slotNode = _mem.GetNode("pet_slot_ptr-index-mul-10-add-10");
            if (slotNode == null) return list;

            uint typeOff = _mem.GetNodeOffset("pet_type");
            uint lvlOff = _mem.GetNodeOffset("pet_level");

            for (int i = 0; i < MaxPetSlots; i++)
            {
                uint slotPtr = _mem.ReadUInt(petBase + slotNode.EvaluateFormula((uint)i));
                if (!MemoryProcess.IsValidPointer(slotPtr)) continue;

                list.Add(new PetSlotInfo
                {
                    Slot = i,
                    Name = _mem.ReadUnicodeStringByChain(slotPtr, "pet_name", 32),
                    Type = _mem.ReadUInt(slotPtr + typeOff),
                    Level = _mem.ReadUInt(slotPtr + lvlOff)
                });
            }

            return list;
        }
    }
}