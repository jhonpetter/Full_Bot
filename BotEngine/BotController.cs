using System;
using System.Diagnostics;
using System.Linq;

namespace Full_Bot
{
    public enum BotState
    {
        Idle,
        Moving,
        Combat,
        Combatendo,
        Gathering,
        Following,
        Dead
    }

    public partial class BotController : IDisposable
    {
        public TreeConfigRoot Config { get; }
        public MemoryProcess Memory { get; private set; }
        public AsmRunner Asm { get; private set; }

        public PlayerCore Player { get; private set; }
        public PlayerBuffs Buffs { get; private set; }
        public PlayerInventory Inventory { get; private set; }
        public PlayerParty Party { get; private set; }
        public PlayerPets Pets { get; private set; }
        public PlayerSkills Skills { get; private set; }
        public PlayerFriends Friends { get; private set; }
        public TasksQuestActive ActiveQuests { get; private set; }
        public TasksQuestNews NewsQuests { get; private set; }
        public TasksLoader Tasks { get; private set; }
        public WorldEntities World { get; private set; }

        public BotCombat Combat { get; private set; }
        public BotQuestRunner Gather { get; private set; }
        public BotPartyFollow Support { get; private set; }

        public BotState CurrentState { get; set; } = BotState.Idle;
        public string StatusText { get; set; } = "Aguardando";
        public bool IsEnabled { get; set; }

        public BotController(string configPath = @"Config\config.json")
        {
            Config = ConfigManager.Load(configPath);
            Tasks = new TasksLoader();
        }

        public bool AttachProcess(Process proc = null)
        {
            DetachProcess();

            string procName = Config.DetectProcessName("elementclient");
            var targetProc = proc ?? Process.GetProcessesByName(procName).FirstOrDefault();
            if (targetProc == null)
            {
                StatusText = $"Processo '{procName}' não encontrado.";
                return false;
            }

            Memory = new MemoryProcess(targetProc, Config);
            Asm = new AsmRunner();

            Player = new PlayerCore(Memory, Asm);
            Buffs = new PlayerBuffs(Memory, Player);
            Inventory = new PlayerInventory(Memory, Player);
            Party = new PlayerParty(Memory, Asm, Player);
            Pets = new PlayerPets(Memory, Player);
            Skills = new PlayerSkills(Memory, Player);
            Friends = new PlayerFriends(Memory, Player);
            ActiveQuests = new TasksQuestActive(Memory, Player, Inventory);
            NewsQuests = new TasksQuestNews(Memory);
            World = new WorldEntities(Memory);

            Combat = new BotCombat(Player, Skills, World);
            Gather = new BotQuestRunner(Player, ActiveQuests, Inventory, World, Tasks, Combat);
            Support = new BotPartyFollow(Player, Party, World);

            StatusText = $"Conectado: {targetProc.ProcessName} (PID {targetProc.Id})";
            return true;
        }

        public void Pulse()
        {
            if (Memory == null || Player == null) return;

            if (Player.Base == 0)
            {
                CurrentState = BotState.Idle;
                StatusText = "Personagem não carregado / deslogado.";
                return;
            }

            if (Player.HP == 0)
            {
                CurrentState = BotState.Dead;
                StatusText = "Personagem morto.";
                return;
            }

            if (!IsEnabled)
            {
                CurrentState = BotState.Idle;
                return;
            }

            Support?.Tick();

            if (Tasks != null && Tasks.HasPendingSteps)
            {
                CurrentState = BotState.Moving;
                Gather?.Tick();
            }
            else
            {
                CurrentState = BotState.Idle;
            }
        }

        public void DetachProcess()
        {
            Asm?.Dispose();
            Asm = null;

            Memory?.Dispose();
            Memory = null;
        }

        public void Dispose()
        {
            DetachProcess();
        }
    }
}