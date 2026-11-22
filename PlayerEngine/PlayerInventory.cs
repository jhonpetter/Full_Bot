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

        private uint GetNodeOffset(string key)
        {
            var node = _mem.GetNode(key);
            if (node == null) return 0u;

            var local = node.GetLocalOffsetsUInt();
            if (local != null && local.Length > 0)
                return local[0];

            return node.HasOwnAddressDisplacement ? node.OwnAddressDisplacement : 0u;
        }

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
            if (pBase == 0u) return list;

            uint containerOff = GetNodeOffset(baseKey);
            if (containerOff == 0u) return list;

            uint invBase = _mem.ReadUInt(pBase + containerOff);
            if (invBase == 0u) return list;

            var baseNode = _mem.GetNode(baseKey);
            var slotNode = _mem.GetNode(slotArrayKey);
            if (slotNode == null) return list;

            uint arrayPtrOff = 0u;
            if (baseNode?.Children != null)
            {
                foreach (var child in baseNode.Children)
                {
                    if (child == null) continue;
                    if (child.Key.Contains("array") && !child.HasFormula)
                    {
                        var l = child.GetLocalOffsetsUInt();
                        if (l != null && l.Length > 0)
                        {
                            arrayPtrOff = l[0];
                            break;
                        }
                    }
                }
            }

            if (arrayPtrOff == 0u) return list;

            uint arrayPtr = _mem.ReadUInt(invBase + arrayPtrOff);
            if (arrayPtr == 0u) return list;

            uint idOff = GetNodeOffset(idKey);
            uint amountOff = GetNodeOffset(amountKey);
            uint maxOff = GetNodeOffset(maxKey);

            var nameNode = _mem.GetNode(nameKey);
            var nameOffs = nameNode?.GetLocalOffsetsUInt();
            if (nameOffs == null || nameOffs.Length == 0) return list;

            for (int i = 0; i < maxSlots; i++)
            {
                uint slotDelta = slotNode.EvaluateFormula((uint)i);
                uint itemPtr = _mem.ReadUInt(arrayPtr + slotDelta);
                if (itemPtr == 0u) continue;

                uint itemId = _mem.ReadUInt(itemPtr + idOff);
                if (itemId == 0u) continue;

                uint namePtr = _mem.ReadUInt(itemPtr + nameOffs[0]);
                for (int n = 1; n < nameOffs.Length && namePtr != 0u; n++)
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