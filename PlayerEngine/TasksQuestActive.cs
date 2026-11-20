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

        public List<ActiveQuestInfo> GetActiveQuests()
        {
            var allEntries = new List<ActiveQuestInfo>();
            uint pBase = _player.Base;
            if (pBase == 0) return allEntries;

            var questRootNode = _mem.GetNode("quest_list_base");
            var rootLocal = questRootNode?.GetLocalOffsetsUInt() ?? new uint[] { 0x14D8, 0x08 };
            uint questRoot = _mem.ResolvePointer(pBase + rootLocal[0], ConfigManager.SkipFirst(rootLocal));
            if (questRoot == 0) return allEntries;

            byte activeCount = _mem.ReadByte(questRoot);
            if (activeCount == 0 || activeCount > MaxActiveQuestsSafety)
                return allEntries;

            var taskSlotNode = _mem.GetNode("quest_task_ptr-index-mul-20-add-8");
            var killedCountNode = _mem.GetNode("quest_killed_mob_count-index-mul-2-add-13");
            var targetSlotNode = _mem.GetNode("quest_target_ptr-index-mul-1e");

            uint parentOff = _mem.GetFirstLocalOffset("quest_parent", 0x02);
            uint statusOff = _mem.GetFirstLocalOffset("quest_status", 0x06);
            uint templatePtrOff = _mem.GetFirstLocalOffset("quest_template_ptr", 0x0D);

            uint nameOff = _mem.GetFirstLocalOffset("quest_template_name", 0x08);
            uint fromNpcOff = _mem.GetFirstLocalOffset("quest_from_npc", 0xF2);
            uint toNpcOff = _mem.GetFirstLocalOffset("quest_to_npc", 0xF6);
            uint targetCountOff = _mem.GetFirstLocalOffset("quest_target_diff_count", 0x353);
            uint targetArrayOff = _mem.GetFirstLocalOffset("quest_target_array", 0x357);

            uint mobIdOff = _mem.GetFirstLocalOffset("quest_target_mob_id", 0x00);
            uint mobsReqOff = _mem.GetFirstLocalOffset("quest_target_kill_count", 0x04);
            uint itemIdOff = _mem.GetFirstLocalOffset("quest_target_item_id", 0x08);
            uint itemsReqOff = _mem.GetFirstLocalOffset("quest_target_pickup_count", 0x0C);

            var questBagItems = _inventory.GetQuestItems();

            for (uint i = 0; i < activeCount; i++)
            {
                uint entryAddr = taskSlotNode != null
                    ? (questRoot + taskSlotNode.EvaluateFormula(i))
                    : (questRoot + 0x08 + (i * 0x20));

                ushort questId = _mem.ReadUShort(entryAddr);
                if (questId == 0 || questId == 0xFFFF) continue;

                byte parentIndex = _mem.ReadByte(entryAddr + parentOff);
                byte status = _mem.ReadByte(entryAddr + statusOff);
                uint templatePtr = _mem.ReadUInt(entryAddr + templatePtrOff);

                var quest = new ActiveQuestInfo
                {
                    SlotIndex = (int)i,
                    QuestId = questId,
                    ParentSlotIndex = parentIndex,
                    Status = status,
                    Name = $"Quest #{questId}"
                };

                if (templatePtr != 0)
                {
                    string rawName = _mem.ReadUnicodeString(templatePtr + nameOff, 64);
                    if (!string.IsNullOrWhiteSpace(rawName))
                        quest.Name = rawName;

                    quest.FromNpcId = _mem.ReadUInt(templatePtr + fromNpcOff);
                    quest.ToNpcId = _mem.ReadUInt(templatePtr + toNpcOff);

                    uint targetCount = _mem.ReadUInt(templatePtr + targetCountOff);
                    uint targetArrayPtr = _mem.ReadUInt(templatePtr + targetArrayOff);

                    if (targetArrayPtr != 0 && targetCount > 0 && targetCount <= MaxTargetsSafety)
                    {
                        for (uint t = 0; t < targetCount; t++)
                        {
                            uint tDelta = targetSlotNode != null ? targetSlotNode.EvaluateFormula(t) : (t * 0x1Eu);
                            uint tAddr = targetArrayPtr + tDelta;

                            uint mobId = _mem.ReadUInt(tAddr + mobIdOff);
                            uint mobsReq = _mem.ReadUInt(tAddr + mobsReqOff);
                            uint itemId = _mem.ReadUInt(tAddr + itemIdOff);
                            uint itemsReq = _mem.ReadUInt(tAddr + itemsReqOff);

                            uint itemsHave = 0;
                            if (itemId > 0)
                            {
                                itemsHave = (uint)questBagItems
                                    .Where(it => it.ItemId == itemId)
                                    .Sum(it => (long)it.Amount);
                            }

                            uint killedOffset = killedCountNode != null
                                ? killedCountNode.EvaluateFormula(t)
                                : (0x13u + (t * 2u));

                            quest.Targets.Add(new QuestTargetRequirement
                            {
                                MobId = mobId,
                                MobsRequired = mobsReq,
                                MobsKilled = _mem.ReadUShort(entryAddr + killedOffset),
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