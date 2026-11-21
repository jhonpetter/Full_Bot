using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public enum CultivationActionType
    {
        None = 0,
        PickQuestFromJournal = 1,
        MoveToQuestLocation = 2,
        HoverHighWaitingParty = 3,
        KillQuestMob = 4,
        SkipBossInSolo = 5,
        HelpLowerLevelParty = 6,
        QuestCompleted = 7
    }

    public class CultivationDecision
    {
        public bool Handled { get; set; }
        public CultivationActionType Action { get; set; } = CultivationActionType.None;
        public BotState DesiredState { get; set; } = BotState.Idle;

        public uint QuestId { get; set; }
        public uint TargetMobId { get; set; }
        public string TargetMobName { get; set; } = string.Empty;
        public bool IsBossTarget { get; set; }
        public bool UseFlight { get; set; }

        public bool HasMoveGoal { get; set; }
        public float MoveX { get; set; }
        public float MoveY { get; set; }
        public float MoveZ { get; set; }

        public SharedQuestGuidance SharedGuidance { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class SelfCultivation
    {
        private static readonly ConcurrentDictionary<uint, uint> _sharedActiveQuestByPlayerId =
            new ConcurrentDictionary<uint, uint>();
        private static readonly ConcurrentDictionary<uint, bool> _sharedArrivedAtQuestByPlayerId =
            new ConcurrentDictionary<uint, bool>();
        private static readonly ConcurrentDictionary<uint, SharedQuestGuidance> _sharedGuidanceByPlayerId =
            new ConcurrentDictionary<uint, SharedQuestGuidance>();

        private readonly PlayerCore _player;
        private readonly PlayerParty _party;
        private readonly WorldEntities _world;

        private DateTime _waitStartedUtc = DateTime.MinValue;
        private uint _trackingQuestId = 0;

        public SelfCultivationConfig Config { get; private set; }
        public event Action<uint> OnRequestPickQuestFromJournal;
        public event Action<SharedQuestGuidance> OnShareQuestGuidance;

        public SelfCultivation(
            PlayerCore player,
            PlayerParty party,
            WorldEntities world,
            SelfCultivationConfig config = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _party = party;
            _world = world;
            Config = config ?? SelfCultivationConfig.CreateDefault();
        }

        public void UpdateConfig(SelfCultivationConfig newConfig)
        {
            if (newConfig != null)
            {
                if (newConfig.Quests == null)
                    newConfig.Quests = new List<CultivationQuestDefinition>();
                Config = newConfig;
            }
        }

        // 1.2.3: 1 único tipo de mob e quantidade = 1 -> Boss
        public bool IsSingleBossQuest(CultivationQuestDefinition quest)
        {
            if (quest == null || quest.Objectives == null || quest.Objectives.Count != 1)
                return false;

            var obj = quest.Objectives[0];
            return obj != null && obj.RequiredCount == 1;
        }

        // 1.2.3: Até N (padrão 5) tipos de mobs diferentes e quantidade de cada tipo = 1
        public bool IsMultiBossQuest(CultivationQuestDefinition quest)
        {
            if (quest == null || quest.Objectives == null)
                return false;

            int maxTypes = Math.Max(2, Config.MaxMultiBossTypesInQuest);
            if (quest.Objectives.Count < 2 || quest.Objectives.Count > maxTypes)
                return false;

            foreach (var obj in quest.Objectives)
            {
                if (obj == null || obj.RequiredCount != 1)
                    return false;
            }

            return true;
        }

        public bool IsBossQuest(CultivationQuestDefinition quest)
        {
            return IsSingleBossQuest(quest) || IsMultiBossQuest(quest);
        }

        public bool AreMembersCloseInLevel(List<PartyMemberInfo> members)
        {
            if (members == null || members.Count <= 1)
                return true;

            var levels = members.Where(m => m != null && m.Id != 0 && m.Level > 0).Select(m => (int)m.Level).ToList();
            if (_player.Level > 0)
                levels.Add((int)_player.Level);

            if (levels.Count <= 1)
                return true;

            int minLvl = levels.Min();
            int maxLvl = levels.Max();
            return (maxLvl - minLvl) <= Math.Max(0, Config.MaxLevelDiffForGroup);
        }

        // 1.2.1 e 1.2.4: Iniciar em grupo acima do nível 18 ou mesmo range de nível
        public bool CanStartQuestInGroup(CultivationQuestDefinition quest, List<PartyMemberInfo> members)
        {
            if (quest == null) return false;
            if ((int)_player.Level < quest.RequiredLevel) return false;

            bool closeLevel = AreMembersCloseInLevel(members);
            bool aboveGroupThreshold = (int)_player.Level >= Config.MinGroupCultivationLevel;

            return closeLevel || aboveGroupThreshold;
        }

        // 1.3: Nível muito acima dos outros na PT -> ignora próprio cultivo e ajuda compartilhando local/mobs
        public bool TryEvaluateHighLevelCarry(List<PartyMemberInfo> members, out CultivationDecision decision)
        {
            decision = null;
            if (!Config.HighLevelHelpOthersEnabled || members == null || members.Count <= 1)
                return false;

            var others = members.Where(m => m != null && m.Id != 0 && m.Id != _player.Id && m.Level > 0).ToList();
            if (others.Count == 0 || _player.Level == 0)
                return false;

            double avgOtherLevel = others.Average(m => (double)m.Level);
            double diff = (double)_player.Level - avgOtherLevel;
            if (diff < Config.HighLevelCarryDiffThreshold)
                return false;

            // Procura orientação compartilhada por outro membro ou usa a primeira quest/objetivo dos outros
            SharedQuestGuidance guidance = null;
            foreach (var other in others)
            {
                if (_sharedGuidanceByPlayerId.TryGetValue(other.Id, out var g) && g != null &&
                    (DateTime.UtcNow - g.UpdatedUtc).TotalSeconds <= 15.0)
                {
                    guidance = g;
                    break;
                }
            }

            if (guidance == null)
            {
                var fallback = others.First();
                guidance = new SharedQuestGuidance
                {
                    HelperPlayerId = _player.Id,
                    HelperName = _player.Name,
                    QuestId = _sharedActiveQuestByPlayerId.TryGetValue(fallback.Id, out var qid) ? qid : 0,
                    TargetMobId = 0,
                    TargetMobName = "Mobs da Quest do Grupo",
                    X = fallback.X,
                    Y = fallback.Y,
                    Z = fallback.Z,
                    UpdatedUtc = DateTime.UtcNow
                };
            }

            if (Config.ShareLocationAndMobsWithParty)
            {
                _sharedGuidanceByPlayerId[_player.Id] = guidance;
                OnShareQuestGuidance?.Invoke(guidance);
            }

            float dist = MoveObstacleAvoidance.Dist2D(_player.X, _player.Z, guidance.X, guidance.Z);
            bool needMove = dist > Config.ArrivalRadius;

            decision = new CultivationDecision
            {
                Handled = true,
                Action = CultivationActionType.HelpLowerLevelParty,
                DesiredState = needMove ? BotState.Moving : BotState.Combat,
                QuestId = guidance.QuestId,
                TargetMobId = guidance.TargetMobId,
                TargetMobName = guidance.TargetMobName,
                HasMoveGoal = needMove,
                MoveX = guidance.X,
                MoveY = guidance.Y,
                MoveZ = guidance.Z,
                SharedGuidance = guidance,
                Status = $"[Lvl Alto -> Ajuda PT] Indo ajudar grupo em ({guidance.X:F0}, {guidance.Z:F0}) - {guidance.TargetMobName}"
            };
            return true;
        }

        private bool ArePartyMembersAtLocation(List<PartyMemberInfo> members, uint questId, float centerX, float centerZ)
        {
            if (members == null || members.Count <= 1) return true;

            foreach (var m in members)
            {
                if (m == null || m.Id == 0 || m.Id == _player.Id) continue;

                bool sameQuest = !_sharedActiveQuestByPlayerId.TryGetValue(m.Id, out uint mq) || mq == 0 || mq == questId;
                if (!sameQuest) return false;

                bool arrivedFlag = _sharedArrivedAtQuestByPlayerId.TryGetValue(m.Id, out bool arr) && arr;
                float dToQuest = MoveObstacleAvoidance.Dist2D(m.X, m.Z, centerX, centerZ);
                if (!arrivedFlag && dToQuest > Config.ArrivalRadius * 1.5f)
                    return false;
            }

            return true;
        }

        public CultivationDecision Step()
        {
            var empty = new CultivationDecision { Handled = false, Action = CultivationActionType.None };
            if (!Config.Enabled || _player == null)
                return empty;

            var members = _party?.GetMembers() ?? new List<PartyMemberInfo>();
            bool inParty = members.Count > 1;

            // 1.3 Nível muito acima dos outros na PT
            if (inParty && TryEvaluateHighLevelCarry(members, out var carryDecision))
                return carryDecision;

            var quest = Config.Quests?.FirstOrDefault(q => q != null && (int)_player.Level >= q.RequiredLevel && !q.Objectives.All(o => o.IsCompleted));
            if (quest == null)
            {
                _sharedActiveQuestByPlayerId.TryRemove(_player.Id, out _);
                _sharedArrivedAtQuestByPlayerId.TryRemove(_player.Id, out _);
                return new CultivationDecision
                {
                    Handled = false,
                    Action = CultivationActionType.QuestCompleted,
                    Status = "Nenhuma quest de cultivo pendente para o nível atual"
                };
            }

            if (_trackingQuestId != quest.QuestId)
            {
                _trackingQuestId = quest.QuestId;
                _waitStartedUtc = DateTime.MinValue;
            }

            if (_player.Id != 0)
                _sharedActiveQuestByPlayerId[_player.Id] = quest.QuestId;

            // 1.2.2 Se não tem a quest pega e está disponível no jornal, pega para fazer junto
            if (!quest.HasQuestActive && quest.AvailableInJournal && Config.AutoPickQuestFromJournal)
            {
                OnRequestPickQuestFromJournal?.Invoke(quest.QuestId);
                return new CultivationDecision
                {
                    Handled = true,
                    Action = CultivationActionType.PickQuestFromJournal,
                    DesiredState = BotState.Idle,
                    QuestId = quest.QuestId,
                    Status = $"Pegando quest '{quest.Title}' ({quest.QuestId}) no jornal para fazer em grupo/solo"
                };
            }

            // 1.2.1 / 1.2.4 Em grupo: verifica elegibilidade de nível para fazer quest em grupo
            bool doInGroup = inParty && CanStartQuestInGroup(quest, members);

            // 1.2.3 Detecção de Boss (único mob qtd 1 ou até 5 tipos diferentes qtd 1 cada)
            bool singleBoss = IsSingleBossQuest(quest);
            bool multiBoss = IsMultiBossQuest(quest);
            bool isBossQuest = singleBoss || multiBoss;

            bool fullParty = members.Count >= Math.Max(2, Config.FullPartySize);
            bool partyReadyForBoss = (!singleBoss || !Config.RequireFullPartyForSingleBoss || fullParty) &&
                                   (!multiBoss || !Config.RequireFullPartyForMultiBoss || fullParty);

            // 1.2.5 Em solo (ou sem PT completa exigida para boss), evita bosses
            if ((!doInGroup || !partyReadyForBoss) && isBossQuest && Config.SoloAvoidBosses)
            {
                return new CultivationDecision
                {
                    Handled = true,
                    Action = CultivationActionType.SkipBossInSolo,
                    DesiredState = BotState.Idle,
                    QuestId = quest.QuestId,
                    IsBossTarget = true,
                    Status = !doInGroup
                        ? $"Quest de boss '{quest.Title}' adiada/evitada no solo"
                        : $"Aguardando PT completa ({members.Count}/{Config.FullPartySize}) para boss da quest '{quest.Title}'"
                };
            }

            float distToQuest = MoveObstacleAvoidance.Dist2D(_player.X, _player.Z, quest.QuestX, quest.QuestZ);
            bool atLocation = distToQuest <= Math.Max(2.0f, Config.ArrivalRadius);

            if (_player.Id != 0)
                _sharedArrivedAtQuestByPlayerId[_player.Id] = atLocation;

            // 1.2.2 Deslocar até o local da quest e aguardar membros voando alto se necessário
            if (!atLocation)
            {
                _waitStartedUtc = DateTime.MinValue;
                return new CultivationDecision
                {
                    Handled = true,
                    Action = CultivationActionType.MoveToQuestLocation,
                    DesiredState = BotState.Moving,
                    QuestId = quest.QuestId,
                    HasMoveGoal = true,
                    MoveX = quest.QuestX,
                    MoveY = quest.QuestY,
                    MoveZ = quest.QuestZ,
                    Status = $"Indo ao local da quest '{quest.Title}' ({distToQuest:F1}m)"
                };
            }

            if (doInGroup && Config.WaitForPartyAtQuestLocation && !ArePartyMembersAtLocation(members, quest.QuestId, quest.QuestX, quest.QuestZ))
            {
                if (_waitStartedUtc == DateTime.MinValue)
                    _waitStartedUtc = DateTime.UtcNow;

                double elapsed = (DateTime.UtcNow - _waitStartedUtc).TotalSeconds;
                if (elapsed < Math.Max(2.0, Config.PartyWaitTimeoutSeconds))
                {
                    bool hoverHigh = Config.PreferHoverHighWhileWaiting;
                    float targetY = hoverHigh ? (quest.QuestY + Math.Max(5.0f, Config.FlyHoverAltitude)) : quest.QuestY;
                    bool needLift = hoverHigh && Math.Abs(_player.Y - targetY) > 2.5f;

                    return new CultivationDecision
                    {
                        Handled = true,
                        Action = CultivationActionType.HoverHighWaitingParty,
                        DesiredState = needLift ? BotState.Moving : BotState.WaitingGroup,
                        QuestId = quest.QuestId,
                        UseFlight = hoverHigh,
                        HasMoveGoal = needLift,
                        MoveX = quest.QuestX,
                        MoveY = targetY,
                        MoveZ = quest.QuestZ,
                        Status = hoverHigh
                            ? $"Voando alto ({targetY:F0}m) no local da quest aguardando grupo ({elapsed:F0}s)..."
                            : $"Aguardando grupo no local da quest ({elapsed:F0}s)..."
                    };
                }
            }

            _waitStartedUtc = DateTime.MinValue;

            var nextObj = quest.Objectives.FirstOrDefault(o => o != null && !o.IsCompleted);
            if (nextObj == null)
            {
                return new CultivationDecision
                {
                    Handled = false,
                    Action = CultivationActionType.QuestCompleted,
                    QuestId = quest.QuestId,
                    Status = $"Objetivos da quest '{quest.Title}' concluídos"
                };
            }

            if (Config.ShareLocationAndMobsWithParty && inParty && _player.Id != 0)
            {
                _sharedGuidanceByPlayerId[_player.Id] = new SharedQuestGuidance
                {
                    HelperPlayerId = _player.Id,
                    HelperName = _player.Name,
                    QuestId = quest.QuestId,
                    TargetMobId = nextObj.MobOrTargetId,
                    TargetMobName = nextObj.TargetName,
                    X = nextObj.X != 0 ? nextObj.X : quest.QuestX,
                    Y = nextObj.Y != 0 ? nextObj.Y : quest.QuestY,
                    Z = nextObj.Z != 0 ? nextObj.Z : quest.QuestZ,
                    UpdatedUtc = DateTime.UtcNow
                };
            }

            return new CultivationDecision
            {
                Handled = true,
                Action = CultivationActionType.KillQuestMob,
                DesiredState = BotState.Combat,
                QuestId = quest.QuestId,
                TargetMobId = nextObj.MobOrTargetId,
                TargetMobName = nextObj.TargetName,
                IsBossTarget = nextObj.IsBoss || isBossQuest,
                HasMoveGoal = nextObj.X != 0 && nextObj.Z != 0 &&
                              MoveObstacleAvoidance.Dist2D(_player.X, _player.Z, nextObj.X, nextObj.Z) > Config.ArrivalRadius,
                MoveX = nextObj.X != 0 ? nextObj.X : quest.QuestX,
                MoveY = nextObj.Y != 0 ? nextObj.Y : quest.QuestY,
                MoveZ = nextObj.Z != 0 ? nextObj.Z : quest.QuestZ,
                Status = $"Cultivo '{quest.Title}': alvo {nextObj.TargetName} ({nextObj.CurrentCount}/{nextObj.RequiredCount})"
            };
        }
    }
}