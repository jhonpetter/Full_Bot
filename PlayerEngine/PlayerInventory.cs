using System.Collections.Generic;

namespace Full_Bot
{
    public class InventoryItemInfo
    {
        public int Slot { get; set; }
        public uint ItemId { get; set; }
        public string Name { get; set; }
        public uint Amount { get; set; }
        public uint MaxAmount { get; set; }
    }

    public class PlayerInventory
    {
        private readonly MemoryProcess _mem;
        private readonly PlayerCore _player;

        public PlayerInventory(MemoryProcess mem, PlayerCore player)
        {
            _mem = mem;
            _player = player;
        }

        public List<InventoryItemInfo> GetBagItems()
            => ReadContainer("inv_items_base", "inv_item_slot_ptr-index-mul-4", "inv_item_id", "inv_item_amount", "inv_item_max", "inv_item_name", 80);

        public List<InventoryItemInfo> GetEquippedItems()
            => ReadContainer("equip_base", "equip_slot_ptr-index-mul-4", "equip_item_id", "equip_item_amount", "equip_item_max", "equip_item_name", 32);

        public List<InventoryItemInfo> GetQuestItems()
            => ReadContainer("inv_quest_base", "inv_quest_slot_ptr-index-mul-4", "inv_quest_item_id", "inv_quest_item_amount", "inv_quest_item_max", "inv_quest_item_name", 40);

        private List<InventoryItemInfo> ReadContainer(
            string baseKey,
            string slotArrayKey,
            string idKey,
            string amountKey,
            string maxKey,
            string nameKey,
            int maxSlots)
        {
            var list = new List<InventoryItemInfo>();
            uint pBase = _player.Base;
            if (pBase == 0) return list;

            uint containerOff = _mem.GetFirstLocalOffset(baseKey, 0);
            if (containerOff == 0) return list;

            uint invBase = _mem.ReadUInt(pBase + containerOff);
            if (invBase == 0) return list;

            var baseNode = _mem.GetNode(baseKey);
            var slotNode = _mem.GetNode(slotArrayKey);

            // Local offsets do array dentro do container (ex: inv_item_array tem local_offsets ["0xC"])
            uint arrayPtrOff = 0x0C;
            if (baseNode?.Children != null)
            {
                foreach (var child in baseNode.Children.Values)
                {
                    if (child.Key.Contains("array") && !child.IsArray)
                    {
                        var l = child.GetLocalOffsetsUInt();
                        if (l.Length > 0) arrayPtrOff = l[0];
                        break;
                    }
                }
            }

            uint arrayPtr = _mem.ReadUInt(invBase + arrayPtrOff);
            if (arrayPtr == 0) return list;

            uint idOff = _mem.GetFirstLocalOffset(idKey, 0x0C);
            uint amountOff = _mem.GetFirstLocalOffset(amountKey, 0x14);
            uint maxOff = _mem.GetFirstLocalOffset(maxKey, 0x18);

            var nameNode = _mem.GetNode(nameKey);
            var nameOffs = nameNode?.GetLocalOffsetsUInt() ?? new uint[] { 0x58, 0x04 };

            for (int i = 0; i < maxSlots; i++)
            {
                uint slotDelta = slotNode != null ? slotNode.EvaluateFormula((uint)i) : (uint)(i * 4);
                uint itemPtr = _mem.ReadUInt(arrayPtr + slotDelta);
                if (itemPtr == 0) continue;

                uint itemId = _mem.ReadUInt(itemPtr + idOff);
                if (itemId == 0) continue;

                uint namePtr = _mem.ReadUInt(itemPtr + nameOffs[0]);
                for (int n = 1; n < nameOffs.Length && namePtr != 0; n++)
                    namePtr = _mem.ReadUInt(namePtr + nameOffs[n]);

                list.Add(new InventoryItemInfo
                {
                    Slot = i,
                    ItemId = itemId,
                    Amount = _mem.ReadUInt(itemPtr + amountOff),
                    MaxAmount = _mem.ReadUInt(itemPtr + maxOff),
                    Name = _mem.ReadUnicodeString(namePtr, 48)
                });
            }
            return list;
        }
    }
}