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
        Gathering,
        Following,
        WaitingGroup,
        Dead
    }

    public partial class BotController : IDisposable
    {
        private readonly object _stateLock = new object();

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

        public SupportModule Support { get; private set; }
        public CombatModule Combat { get; private set; }
        public GatherModule Gather { get; private set; }
        public BotRoutines Routines { get; private set; }

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

            Support = new SupportModule(Player, Buffs, Skills, Party, Friends);
            Combat = new CombatModule(Player, Skills, World);
            Gather = new GatherModule(Player, World);
            Routines = new BotRoutines(Player, Skills, World, Combat, Gather);

            InitializeNewModules();

            StatusText = $"Conectado: {targetProc.ProcessName} (PID {targetProc.Id})";
            return true;
        }

        public void Pulse()
        {
            lock (_stateLock)
            {
                if (Memory == null || Player == null) return;

                try
                {
                    if (Memory.Process == null || Memory.Process.HasExited)
                    {
                        CurrentState = BotState.Idle;
                        StatusText = "Processo finalizado.";
                        return;
                    }

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
                        StatusText = "Desligado (aguardando).";
                        return;
                    }

                    if (RunIntegratedModulesStep())
                        return;

                    // 1. Grupo e suporte
                    if (Support != null)
                    {
                        if (Support.TryAcceptPartyInvite(out string acceptStatus))
                        {
                            CurrentState = BotState.Following;
                            StatusText = acceptStatus;
                            return;
                        }

                        if (Support.TryAutoInviteFriends(out string inviteStatus))
                        {
                            StatusText = inviteStatus;
                        }

                        if (Support.TryMaintainProximityToLeader(out string followStatus))
                        {
                            CurrentState = BotState.Following;
                            StatusText = followStatus;
                            return;
                        }

                        if (Support.ShouldWaitForGroup(out string waitStatus))
                        {
                            CurrentState = BotState.WaitingGroup;
                            StatusText = waitStatus;
                            return;
                        }
                    }

                    // 2. Área, perímetro e deslocamento/voo
                    if (Routines != null && Routines.RunAreaAndFlightRoutine(out BotState moveState, out string moveStatus))
                    {
                        CurrentState = moveState;
                        StatusText = moveStatus;
                        return;
                    }

                    // 3. Buffs próprios e de aliados próximos
                    if (Support != null && Support.TryProcessBuffs(out string buffStatus))
                    {
                        StatusText = buffStatus;
                        return;
                    }

                    // 4. Loot pós-combate próximo
                    if (Routines != null && Routines.RunLootRoutine(out string lootStatus))
                    {
                        CurrentState = BotState.Gathering;
                        StatusText = lootStatus;
                        return;
                    }

                    // 5. Defesa, auto-mobada, lista de IDs e ajuda a aliados
                    if (Routines != null && Routines.RunCombatAndAggroRoutine(out string combatStatus))
                    {
                        CurrentState = BotState.Combat;
                        StatusText = combatStatus;
                        return;
                    }

                    if (Combat != null && Combat.TryHelpOtherBots(out string helpStatus))
                    {
                        CurrentState = BotState.Combat;
                        StatusText = helpStatus;
                        return;
                    }

                    CurrentState = BotState.Idle;
                    StatusText = "Ativo / aguardando ação.";
                }
                catch (Exception ex)
                {
                    StatusText = $"Erro no ciclo: {ex.Message}";
                }
            }
        }

        public void DetachProcess()
        {
            lock (_stateLock)
            {
                Asm?.Dispose();
                Asm = null;

                Memory?.Dispose();
                Memory = null;
            }
        }

        public void Dispose()
        {
            DetachProcess();
        }
    }
}