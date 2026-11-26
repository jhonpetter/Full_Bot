using System;
using System.Diagnostics;

namespace Full_Bot
{
    public class PlayerSession : IDisposable
    {
        public string ConfigPath { get; }
        public CheatTableDocument Table { get; }
        public MemoryProcess Memory { get; }
        public AsmRunner Asm { get; }

        public PlayerCore Player { get; }
        public PartyCore PartyCore { get; }
        public PlayerBuffs Buffs { get; }
        public PlayerFriends Friends { get; }
        public PlayerInventory Inventory { get; }
        public PlayerPets Pets { get; }
        public PlayerSkills Skills { get; }
        public WorldEntities World { get; }
        public TasksQuestActive ActiveQuests { get; }
        public TasksQuestNews NewsQuests { get; }

        public bool IsAlive => Memory?.Process != null && !Memory.Process.HasExited;

        public PlayerSession(string configPath, Process process, CheatTableDocument table)
        {
            ConfigPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            if (process == null) throw new ArgumentNullException(nameof(process));
            Table = table ?? throw new ArgumentNullException(nameof(table));

            Memory = new MemoryProcess(process, Table);
            Asm = new AsmRunner();

            Player = new PlayerCore(Memory, Asm);
            PartyCore = new PartyCore(Memory, Player);
            Buffs = new PlayerBuffs(Memory, Player);
            Friends = new PlayerFriends(Memory, Player);
            Inventory = new PlayerInventory(Memory, Player);
            Pets = new PlayerPets(Memory, Player);
            Skills = new PlayerSkills(Memory, Player, Asm); //new PlayerSkills(Memory, Player);
            World = new WorldEntities(Memory, Player);
            ActiveQuests = new TasksQuestActive(Memory, Player, Inventory);
            NewsQuests = new TasksQuestNews(Memory);

            Player.AttachModules(
                buffs: Buffs,
                friends: Friends,
                inventory: Inventory,
                partyCore: PartyCore,
                pets: Pets,
                skills: Skills,
                world: World,
                activeQuests: ActiveQuests,
                newsQuests: NewsQuests
            );
        }

        public void Dispose()
        {
            try { Memory?.Dispose(); }
            catch { }
        }
    }
}