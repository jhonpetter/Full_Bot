using System;
using System.Collections.Generic;
using System.IO;

namespace Full_Bot
{
    public class NpcShopBagInfo
    {
        public uint BagIndex { get; set; }
        public uint SlotCount { get; set; }
        public uint CumulativeSlotsBefore { get; set; }
        public uint BagStructPtr { get; set; }
        public uint ArrayPtr { get; set; }
        public bool IsValid { get; set; }
    }

    public class NpcShopItemInfo
    {
        public uint BagIndex { get; set; }
        public uint ItemIndexInBag { get; set; }
        public uint AccumulatedSlotsBeforeBag { get; set; }
        public uint ComputedNpcSlot { get; set; }
        public uint ItemId { get; set; }
        public string Name { get; set; }
        public uint UnitPrice { get; set; }
        public uint PriceByAmount { get; set; }
        public uint MaxAmount { get; set; }
    }

    public class PlayerNpcTradeFunctions
    {
        private const string ScriptsFolder = "Scripts";
        private const string BuyScriptFile = "BuyItem.asm";
        private const string SellScriptFile = "SellItem.asm";

        private const uint MaxNpcBagsTotal = 8u; // Bags 0..7
        private const uint MaxNpcShopEntriesSafety = 256u;

        // Ajuste aplicado ao final de (accumulatedSlots + itemIndexInBag):
        // Ex.: -1 para compensar deslocamento de índice; ajuste conforme o teste (-1, -2, etc.).
        private const int SlotIndexAdjustment = 0;

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;
        private readonly PlayerCore _player;

        public PlayerNpcTradeFunctions(MemoryProcess mem, AsmRunner asm, PlayerCore player)
        {
            _mem = mem ?? throw new ArgumentNullException(nameof(mem));
            _asm = asm ?? throw new ArgumentNullException(nameof(asm));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        private static string ResolveScriptPath(string fileName)
        {
            string baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ScriptsFolder, fileName);
            if (File.Exists(baseDirPath))
                return baseDirPath;

            return Path.Combine(ScriptsFolder, fileName);
        }

        private uint GetPlayerBasePtr()
        {
            uint pBase = _player?.Base ?? _mem.ResolveStructBase("player_struct");
            return MemoryProcess.IsValidPointer(pBase) ? pBase : 0u;
        }

        private static uint ApplySlotAdjustment(uint accumulatedBefore, uint itemIndexInBag, int adjustment)
        {
            long raw = (long)accumulatedBefore + itemIndexInBag + adjustment;
            return raw > 0L ? (uint)raw : 0u;
        }

        /// <summary>
        /// Lê as bags 0..7 na ordem e acumula inv_items_npc_slots de cada bag (8, 16, 32, etc.),
        /// mantendo a ordem dos índices 0..7 mesmo quando uma bag não tem itens carregados.
        /// </summary>
        public List<NpcShopBagInfo> GetNpcBagsOrdered()
        {
            var bags = new List<NpcShopBagInfo>();
            uint pBase = GetPlayerBasePtr();
            if (!MemoryProcess.IsValidPointer(pBase))
                return bags;

            var bagBaseNode = _mem.GetNode("inv_items_npc_base-index-mul-4-add-108C");
            var slotsNode = _mem.GetNode("inv_items_npc_slots");
            var arrayNode = _mem.GetNode("inv_items_npc_array");
            if (bagBaseNode == null || slotsNode == null || arrayNode == null)
                return bags;

            uint slotsOff = _mem.GetNodeOffset("inv_items_npc_slots");
            uint arrayOff = _mem.GetNodeOffset("inv_items_npc_array");

            uint accumulatedSlots = 0u;

            for (uint bagIdx = 0u; bagIdx < MaxNpcBagsTotal; bagIdx++)
            {
                uint bagBaseOff = bagBaseNode.EvaluateFormula(bagIdx);
                uint bagStructPtr = _mem.ReadUInt(unchecked(pBase + bagBaseOff));

                uint slotCount = 0u;
                uint arrayPtr = 0u;
                bool isValid = false;

                if (MemoryProcess.IsValidPointer(bagStructPtr))
                {
                    uint rawSlots = _mem.ReadUInt(unchecked(bagStructPtr + slotsOff));
                    if (rawSlots > 0u && rawSlots <= MaxNpcShopEntriesSafety)
                        slotCount = rawSlots;

                    uint rawArray = _mem.ReadUInt(unchecked(bagStructPtr + arrayOff));
                    if (MemoryProcess.IsValidPointer(rawArray))
                        arrayPtr = rawArray;

                    isValid = slotCount > 0u && arrayPtr != 0u;
                }

                bags.Add(new NpcShopBagInfo
                {
                    BagIndex = bagIdx,
                    SlotCount = slotCount,
                    CumulativeSlotsBefore = accumulatedSlots,
                    BagStructPtr = bagStructPtr,
                    ArrayPtr = arrayPtr,
                    IsValid = isValid
                });

                // Se não achou na bag atual, já soma o total de slots dela para ir para a próxima bag (0..7).
                if (slotCount > 0u)
                    accumulatedSlots = unchecked(accumulatedSlots + slotCount);
            }

            return bags;
        }

        private string ReadNpcItemName(uint itemPtr)
        {
            if (!MemoryProcess.IsValidPointer(itemPtr))
                return string.Empty;

            string byChain = _mem.ReadUnicodeStringByChain(itemPtr, "inv_items_npc_item_name", 64);
            if (!string.IsNullOrEmpty(byChain))
                return byChain;

            var nameNode = _mem.GetNode("inv_items_npc_item_name");
            var offs = nameNode?.GetLocalOffsetsUInt();
            if (offs == null || offs.Length == 0)
                return string.Empty;

            uint cur = itemPtr;
            for (int i = 0; i < offs.Length; i++)
            {
                cur = _mem.ReadUInt(unchecked(cur + offs[i]));
                if (!MemoryProcess.IsValidPointer(cur) && i < offs.Length - 1)
                    return string.Empty;
            }

            return MemoryProcess.IsValidPointer(cur) ? _mem.ReadUnicodeString(cur, 64) : string.Empty;
        }

        /// <summary>
        /// Procura o item da bag 0 até a 7: se não achar na bag atual, acumula o inv_items_npc_slots
        /// dela e segue para a próxima; ao achar no índice i, soma com o acumulado e aplica o ajuste.
        /// </summary>
        public NpcShopItemInfo FindShopItem(uint itemId, int slotAdjustment = SlotIndexAdjustment)
        {
            if (itemId == 0u)
                return null;

            var bags = GetNpcBagsOrdered();
            if (bags.Count == 0)
                return null;

            var slotNode = _mem.GetNode("inv_items_npc_item_slot-index-mul-4");
            if (slotNode == null)
                return null;

            uint idOff = _mem.GetNodeOffset("inv_items_npc_item_id");
            uint maxOff = _mem.GetNodeOffset("inv_items_npc_item_amount_max");
            uint priceByAmtOff = _mem.GetNodeOffset("inv_items_npc_item_price_by_amount");
            uint priceOff = _mem.GetNodeOffset("inv_items_npc_item_price");

            foreach (var bag in bags)
            {
                if (!bag.IsValid)
                    continue;

                for (uint i = 0u; i < bag.SlotCount; i++)
                {
                    uint itemPtr = _mem.ReadUInt(unchecked(bag.ArrayPtr + slotNode.EvaluateFormula(i)));
                    if (!MemoryProcess.IsValidPointer(itemPtr))
                        continue;

                    uint currentId = _mem.ReadUInt(unchecked(itemPtr + idOff));
                    if (currentId != itemId)
                        continue;

                    uint computedSlot = ApplySlotAdjustment(bag.CumulativeSlotsBefore, i, slotAdjustment);

                    return new NpcShopItemInfo
                    {
                        BagIndex = bag.BagIndex,
                        ItemIndexInBag = i,
                        AccumulatedSlotsBeforeBag = bag.CumulativeSlotsBefore,
                        ComputedNpcSlot = computedSlot,
                        ItemId = currentId,
                        Name = ReadNpcItemName(itemPtr),
                        UnitPrice = _mem.ReadUInt(unchecked(itemPtr + priceOff)),
                        PriceByAmount = _mem.ReadUInt(unchecked(itemPtr + priceByAmtOff)),
                        MaxAmount = _mem.ReadUInt(unchecked(itemPtr + maxOff))
                    };
                }
            }

            return null;
        }

        public bool BuyItem(uint itemId, uint amount, int? slotAdjustment = null)
        {
            if (itemId == 0u || amount == 0u)
                return false;

            int adj = slotAdjustment ?? SlotIndexAdjustment;
            var shopItem = FindShopItem(itemId, adj);
            if (shopItem == null)
                return false;

            var vars = new Dictionary<string, uint>
            {
                { "ITEM_ID", itemId },
                { "NPC_SLOT", shopItem.ComputedNpcSlot },
                { "ITEM_AMOUNT", amount }
            };

            _asm.RunScript(_mem, ResolveScriptPath(BuyScriptFile), vars);
            return true;
        }

        public bool SellItem(uint itemId, uint amount)
        {
            if (itemId == 0u || amount == 0u || _player == null)
                return false;

            var bagItems = _player.GetBagItems();
            if (bagItems == null || bagItems.Count == 0)
                return false;

            foreach (var item in bagItems)
            {
                if (item == null || item.ItemId != itemId || item.Amount < amount)
                    continue;

                var vars = new Dictionary<string, uint>
                {
                    { "ITEM_ID", itemId },
                    { "BAG_SLOT", (uint)item.Slot },
                    { "ITEM_AMOUNT", amount },
                    { "UNIT_PRICE", item.Price }
                };

                _asm.RunScript(_mem, ResolveScriptPath(SellScriptFile), vars);
                return true;
            }

            return false;
        }
    }

    public partial class PlayerCore
    {
        private PlayerNpcTradeFunctions _npcTrade;

        public PlayerNpcTradeFunctions NpcTrade
        {
            get
            {
                if (_npcTrade == null)
                    _npcTrade = new PlayerNpcTradeFunctions(_mem, _asm, this);
                return _npcTrade;
            }
        }

        public List<NpcShopBagInfo> GetNpcShopBags()
        {
            return NpcTrade.GetNpcBagsOrdered();
        }

        public NpcShopItemInfo FindNpcShopItem(uint itemId, int? slotAdjustment = null)
        {
            return NpcTrade.FindShopItem(itemId, slotAdjustment ?? -1);
        }

        public bool BuyItem(uint itemId, uint amount, int? slotAdjustment = null)
        {
            return NpcTrade.BuyItem(itemId, amount, slotAdjustment);
        }

        public bool SellItem(uint itemId, uint amount)
        {
            return NpcTrade.SellItem(itemId, amount);
        }
    }
}