using System.Collections.Generic;

namespace Full_Bot
{
    public class AvailableQuestInfo
    {
        public uint QuestId { get; set; }
        public string Name { get; set; }
        public uint RequiredLevel { get; set; }
        public uint FromNpcId { get; set; }
        public uint ToNpcId { get; set; }
    }

    public class TasksQuestNews
    {
        private readonly MemoryProcess _mem;

        public TasksQuestNews(MemoryProcess mem)
        {
            _mem = mem;
        }

        public List<AvailableQuestInfo> GetAvailableQuests(int maxEntries = 60)
        {
            var list = new List<AvailableQuestInfo>();
            uint arrayPtr = 0;

            var newsArrayNode = _mem.GetNode("quest_news_array");
            if (newsArrayNode != null)
            {
                var offs = newsArrayNode.GetOffsetsUInt();
                if (offs.Length > 0)
                    arrayPtr = _mem.ResolvePointer(_mem.GetNodeBaseAddress(newsArrayNode), offs);
            }

            if (arrayPtr == 0)
            {
                var questBaseNode = _mem.GetNode("var_unfreezer") ?? _mem.GetNode("quest_news_array");
                uint questGlobal = _mem.ReadUInt(_mem.GetNodeBaseAddress(questBaseNode));
                if (questGlobal == 0) return list;

                uint newsOff = _mem.GetFirstLocalOffset("quest_news_array", 0x368);
                arrayPtr = _mem.ReadUInt(questGlobal + newsOff);
            }

            if (arrayPtr == 0) return list;

            var slotNode = _mem.GetNode("quest_news_slot_ptr-index-mul-4");
            uint idOff = _mem.GetFirstLocalOffset("quest_news_id", 0x04);
            uint nameOff = _mem.GetFirstLocalOffset("quest_news_name", 0x08);
            uint lvlOff = _mem.GetFirstLocalOffset("quest_news_req_level", 0xEC);
            uint fromNpcOff = _mem.GetFirstLocalOffset("quest_news_from_npc", 0xF2);
            uint toNpcOff = _mem.GetFirstLocalOffset("quest_news_to_npc", 0xF6);

            for (int i = 0; i < maxEntries; i++)
            {
                uint delta = slotNode != null ? slotNode.EvaluateFormula((uint)i) : (uint)(i * 4);
                uint tplPtr = _mem.ReadUInt(arrayPtr + delta);
                if (tplPtr == 0) break;

                uint qId = _mem.ReadUInt(tplPtr + idOff);
                if (qId == 0) continue;

                list.Add(new AvailableQuestInfo
                {
                    QuestId = qId,
                    Name = _mem.ReadUnicodeString(tplPtr + nameOff, 64),
                    RequiredLevel = _mem.ReadUInt(tplPtr + lvlOff),
                    FromNpcId = _mem.ReadUInt(tplPtr + fromNpcOff),
                    ToNpcId = _mem.ReadUInt(tplPtr + toNpcOff)
                });
            }
            return list;
        }
    }
}