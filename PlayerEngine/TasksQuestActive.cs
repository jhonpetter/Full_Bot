using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public class QuestTargetRequirement
    {
        public uint MobId { get; set; }
        public uint MobsRequired { get; set; }
        public uint MobsKilled { get; set; }

        public uint ItemId { get; set; }
        public uint ItemsRequired { get; set; }
        public uint ItemsCollected { get; set; }
    }

    public class ActiveQuestInfo
    {
        public int SlotIndex { get; set; }
        public ushort QuestId { get; set; }
        public byte ParentSlotIndex { get; set; }
        public byte Status { get; set; }
        public string Name { get; set; }
        public uint FromNpcId { get; set; }
        public uint ToNpcId { get; set; }

        public List<QuestTargetRequirement> Targets { get; set; } = new List<QuestTargetRequirement>();
        public List<ActiveQuestInfo> Children { get; set; } = new List<ActiveQuestInfo>();

        public bool IsRoot => ParentSlotIndex == 255;
        public bool IsReadyToTurnIn => Status == 3;
    }

    public class TasksQuestActive
    {
        private const byte MaxActiveQuestsSafety = 40;
        private const uint MaxTargetsSafety = 8;

        private readonly MemoryProcess _mem;
        private readonly PlayerCore _player;
        private readonly PlayerInventory _inventory;

        public TasksQuestActive(MemoryProcess mem, PlayerCore player, PlayerInventory inventory)
        {
            _mem = mem;
            _player = player;
            _inventory = inventory;
        }

        private uint GetNodeOffset(string key)
        {
            var node = _mem.GetNode(key);
            if (node == null) return 0u;

            var local = node.GetLocalOffsetsUInt();
            if (local != null && local.Length > 0)
                return local[0];

            return node.HasOwnAddressDisplacement ? node.OwnAddressDisplacement : 0u;
        }

        public List<ActiveQuestInfo> GetActiveQuests()
        {
            var allEntries = new List<ActiveQuestInfo>();
            uint pBase = _player.Base;
            if (pBase == 0u) return allEntries;

            var questRootOffs = _mem.GetNode("quest_list_base")?.GetLocalOffsetsUInt();
            if (questRootOffs == null || questRootOffs.Length == 0) return allEntries;

            // Ex: pBase -> +14D8 -> +8 => ponteiro base da lista de quests ativas (ex: 3219F8B8)
            uint questRoot = _mem.ReadUInt(pBase + questRootOffs[0]);
            for (int r = 1; r < questRootOffs.Length && questRoot != 0u; r++)
                questRoot = _mem.ReadUInt(questRoot + questRootOffs[r]);

            if (questRoot == 0u) return allEntries;

            byte activeCount = _mem.ReadByte(questRoot);
            if (activeCount == 0 || activeCount > MaxActiveQuestsSafety)
                return allEntries;

            var taskSlotNode = _mem.GetNode("quest_task_ptr-index-mul-20-add-8");
            var killedCountNode = _mem.GetNode("quest_killed_mob_count-index-mul-2-add-13");
            var targetSlotNode = _mem.GetNode("quest_target_ptr-index-mul-1e");

            if (taskSlotNode == null) return allEntries;

            uint questIdOff = GetNodeOffset("quest_id");
            uint parentOff = GetNodeOffset("quest_parent");
            uint statusOff = GetNodeOffset("quest_status");
            uint templatePtrOff = GetNodeOffset("quest_template_ptr");

            uint nameOff = GetNodeOffset("quest_template_name");
            uint fromNpcOff = GetNodeOffset("quest_from_npc");
            uint toNpcOff = GetNodeOffset("quest_to_npc");
            uint targetCountOff = GetNodeOffset("quest_target_diff_count");
            uint targetArrayOff = GetNodeOffset("quest_target_array");

            uint mobIdOff = GetNodeOffset("quest_target_mob_id");
            uint mobsReqOff = GetNodeOffset("quest_target_kill_count");
            uint itemIdOff = GetNodeOffset("quest_target_item_id");
            uint itemsReqOff = GetNodeOffset("quest_target_pickup_count");

            var questBagItems = _inventory.GetQuestItems();

            for (uint i = 0; i < activeCount; i++)
            {
                // Deslocamento direto na memória atual a partir de questRoot (ex: +0*20+8)
                uint entryAddr = questRoot + taskSlotNode.EvaluateFormula(i);

                ushort questId = _mem.ReadUShort(entryAddr + questIdOff);
                if (questId == 0 || questId == 0xFFFF) continue;

                byte parentIndex = _mem.ReadByte(entryAddr + parentOff);
                byte status = _mem.ReadByte(entryAddr + statusOff);

                // Ponteiro do template em entryAddr + templatePtrOff (+D)
                uint templatePtr = _mem.ReadUInt(entryAddr + templatePtrOff);

                var quest = new ActiveQuestInfo
                {
                    SlotIndex = (int)i,
                    QuestId = questId,
                    ParentSlotIndex = parentIndex,
                    Status = status,
                    Name = $"Quest #{questId}"
                };

                if (templatePtr != 0u)
                {
                    string rawName = _mem.ReadUnicodeString(templatePtr + nameOff, 64);
                    if (!string.IsNullOrWhiteSpace(rawName))
                        quest.Name = rawName;

                    quest.FromNpcId = _mem.ReadUInt(templatePtr + fromNpcOff);
                    quest.ToNpcId = _mem.ReadUInt(templatePtr + toNpcOff);

                    uint targetCount = _mem.ReadUInt(templatePtr + targetCountOff);
                    uint targetArrayPtr = _mem.ReadUInt(templatePtr + targetArrayOff);

                    if (targetArrayPtr != 0u && targetCount > 0u && targetCount <= MaxTargetsSafety)
                    {
                        for (uint t = 0; t < targetCount; t++)
                        {
                            uint tDelta = targetSlotNode != null ? targetSlotNode.EvaluateFormula(t) : 0u;
                            uint tAddr = targetArrayPtr + tDelta;

                            uint mobId = _mem.ReadUInt(tAddr + mobIdOff);
                            uint mobsReq = _mem.ReadUInt(tAddr + mobsReqOff);
                            uint itemId = _mem.ReadUInt(tAddr + itemIdOff);
                            uint itemsReq = _mem.ReadUInt(tAddr + itemsReqOff);

                            uint itemsHave = 0u;
                            if (itemId > 0u)
                            {
                                itemsHave = (uint)questBagItems
                                    .Where(it => it.ItemId == itemId)
                                    .Sum(it => (long)it.Amount);
                            }

                            // Deslocamento da contagem de mobs mortos no entryAddr atual (ex: +0*2+13)
                            uint killedOffset = killedCountNode != null ? killedCountNode.EvaluateFormula(t) : 0u;
                            uint mobsKilled = killedCountNode != null ? _mem.ReadUShort(entryAddr + killedOffset) : 0u;

                            quest.Targets.Add(new QuestTargetRequirement
                            {
                                MobId = mobId,
                                MobsRequired = mobsReq,
                                MobsKilled = mobsKilled,
                                ItemId = itemId,
                                ItemsRequired = itemsReq,
                                ItemsCollected = itemsHave
                            });
                        }
                    }
                }

                allEntries.Add(quest);
            }

            return allEntries;
        }

        public List<ActiveQuestInfo> GetActiveQuestTree()
        {
            var all = GetActiveQuests();
            var bySlot = all.ToDictionary(q => q.SlotIndex, q => q);
            var roots = new List<ActiveQuestInfo>();

            foreach (var q in all)
            {
                if (!q.IsRoot && bySlot.TryGetValue(q.ParentSlotIndex, out var parent) && parent != q)
                    parent.Children.Add(q);
                else
                    roots.Add(q);
            }

            return roots.Where(r => IsVisibleQuestNode(r)).ToList();
        }

        private bool IsVisibleQuestNode(ActiveQuestInfo q)
        {
            q.Children = q.Children.Where(c => IsVisibleQuestNode(c)).ToList();
            if (q.Children.Count > 0)
                return true;

            bool hasValidStatus = (q.Status == 2 || q.Status == 3);
            bool hasRealObjective = (q.Targets.Count > 0 || q.ToNpcId > 0 || q.FromNpcId > 0);

            return hasValidStatus && hasRealObjective;
        }
    }
}