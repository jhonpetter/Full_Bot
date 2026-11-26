using System.Collections.Generic;

namespace Full_Bot
{
    public class ActiveQuestTargetInfo
    {
        public uint MobId { get; set; }
        public uint MobsKilled { get; set; }
        public uint MobsRequired { get; set; }
        public uint ItemId { get; set; }
        public uint ItemsCollected { get; set; }
        public uint ItemsRequired { get; set; }
    }

    public class NewsQuestInfo
    {
        public uint QuestId { get; set; }
        public string Name { get; set; } = string.Empty;
        public uint RequiredLevel { get; set; }
        public uint FromNpcId { get; set; }
        public uint ToNpcId { get; set; }
    }

    public class ActiveQuests
    {
        private readonly MemoryProcess _mem;
        private readonly PlayerCore _player;

        public ActiveQuests(MemoryProcess mem, PlayerCore player)
        {
            _mem = mem;
            _player = player;
        }

        public List<ActiveQuestInfo> GetActiveQuestTree()
        {
            var list = new List<ActiveQuestInfo>();
            if (_mem == null || _player == null || _player.Base == 0u)
                return list;

            // Pronto para preencher com os nós de quests ativas do config.CT
            return list;
        }
    }

    public class NewsQuests
    {
        private readonly MemoryProcess _mem;
        private readonly PlayerCore _player;

        public NewsQuests(MemoryProcess mem, PlayerCore player)
        {
            _mem = mem;
            _player = player;
        }

        public List<NewsQuestInfo> GetAvailableQuests()
        {
            var list = new List<NewsQuestInfo>();
            if (_mem == null || _player == null || _player.Base == 0u)
                return list;

            // Pronto para preencher com os nós de quests disponíveis do config.CT
            return list;
        }
    }
}