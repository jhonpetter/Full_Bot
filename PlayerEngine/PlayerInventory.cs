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
        public uint Price { get; set; }
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
            => ReadContainer("inv_items_base", "inv_item_slot_ptr-index-mul-4", "inv_item_id", "inv_item_amount", "inv_item_max", "inv_item_name", 80, "inv_item_price");

        public List<InventoryItemInfo> GetEquippedItems()
            => ReadContainer("equip_base", "equip_slot_ptr-index-mul-4", "equip_item_id", "equip_item_amount", "equip_item_max", "equip_item_name", 32);

        public List<InventoryItemInfo> GetQuestItems()
            => ReadContainer("inv_quest_base", "inv_quest_slot_ptr-index-mul-4", "inv_quest_item_id", "inv_quest_item_amount", "inv_quest_item_max", "inv_quest_item_name", 40);

        private uint FindArrayOffset(CtEntryNode baseNode)
        {
            if (baseNode?.Children == null) return 0u;

            foreach (var child in baseNode.Children)
            {
                if (child == null) continue;
                if (child.Key.Contains("array") && !child.HasFormula)
                {
                    var offs = child.GetLocalOffsetsUInt();
                    if (offs != null && offs.Length > 0)
                        return offs[0];
                }
            }

            return 0u;
        }

        private List<InventoryItemInfo> ReadContainer(
            string baseKey,
            string slotArrayKey,
            string idKey,
            string amountKey,
            string maxKey,
            string nameKey,
            int maxSlots,
            string priceKey = null)
        {
            var list = new List<InventoryItemInfo>();
            uint pBase = _player.Base;
            if (!MemoryProcess.IsValidPointer(pBase)) return list;

            uint invBase = _mem.ReadUIntFromNodeAtBase(pBase, baseKey);
            if (!MemoryProcess.IsValidPointer(invBase)) return list;

            var baseNode = _mem.GetNode(baseKey);
            var slotNode = _mem.GetNode(slotArrayKey);
            if (slotNode == null) return list;

            uint arrayPtrOff = FindArrayOffset(baseNode);
            if (arrayPtrOff == 0u) return list;

            uint arrayPtr = _mem.ReadUInt(invBase + arrayPtrOff);
            if (!MemoryProcess.IsValidPointer(arrayPtr)) return list;

            uint idOff = _mem.GetNodeOffset(idKey);
            uint amountOff = _mem.GetNodeOffset(amountKey);
            uint maxOff = _mem.GetNodeOffset(maxKey);
            bool hasPrice = !string.IsNullOrWhiteSpace(priceKey) && _mem.GetNode(priceKey) != null;
            uint priceOff = hasPrice ? _mem.GetNodeOffset(priceKey) : 0u;

            for (int i = 0; i < maxSlots; i++)
            {
                uint itemPtr = _mem.ReadUInt(arrayPtr + slotNode.EvaluateFormula((uint)i));
                if (!MemoryProcess.IsValidPointer(itemPtr)) continue;

                uint itemId = _mem.ReadUInt(itemPtr + idOff);
                if (itemId == 0u) continue;

                list.Add(new InventoryItemInfo
                {
                    Slot = i,
                    ItemId = itemId,
                    Amount = _mem.ReadUInt(itemPtr + amountOff),
                    MaxAmount = _mem.ReadUInt(itemPtr + maxOff),
                    Price = hasPrice ? _mem.ReadUInt(itemPtr + priceOff) : 0u,
                    Name = _mem.ReadUnicodeStringByChain(itemPtr, nameKey, 48)
                });
            }

            return list;
        }
    }
}