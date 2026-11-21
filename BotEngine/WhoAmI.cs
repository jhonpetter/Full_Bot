using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Full_Bot
{
    public enum WhoAmIActionType
    {
        None = 0,
        SoloDefaultRoutine = 1,
        AdvanceOrEngage = 2,
        FollowTank = 3,
        WaitForOrder = 4,
        KiteOrKeepDistance = 5,
        SupportFlee = 6,
        RescueSupport = 7,
        CastSpecializedSkill = 8,
        ReadyToAttack = 9
    }

    public class SupportAlertState
    {
        public uint SupportPlayerId { get; set; }
        public uint ThreatMobId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public class WhoAmIDecision
    {
        public WhoAmIActionType Action { get; set; } = WhoAmIActionType.None;
        public EncounterContext Context { get; set; } = EncounterContext.SoloOpenWorld;
        public BotState DesiredState { get; set; } = BotState.Idle;

        public bool AllowAttack { get; set; }
        public bool AllowSoloDefaultRoutine { get; set; }
        public bool SupportNeedsRescue { get; set; }
        public uint RescueTargetMobId { get; set; }
        public uint SpecializedSkillToCast { get; set; }
        public uint SpecializedSkillTargetId { get; set; }

        public bool HasMoveGoal { get; set; }
        public float MoveX { get; set; }
        public float MoveY { get; set; }
        public float MoveZ { get; set; }

        public string Status { get; set; } = string.Empty;
    }

    public class WhoAmI
    {
        private static readonly ConcurrentDictionary<uint, WhoAmIRole> _sharedRolesByPlayerId =
            new ConcurrentDictionary<uint, WhoAmIRole>();
        private static readonly ConcurrentDictionary<uint, SupportAlertState> _sharedSupportAlerts =
            new ConcurrentDictionary<uint, SupportAlertState>();

        private readonly PlayerCore _player;
        private readonly PlayerParty _party;
        private readonly WorldEntities _world;
        private readonly Dictionary<string, DateTime> _cooldowns = new Dictionary<string, DateTime>();

        public WhoAmIConfig Config { get; private set; }

        public WhoAmI(
            PlayerCore player,
            PlayerParty party,
            WorldEntities world,
            WhoAmIConfig config = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _party = party;
            _world = world;
            Config = config ?? new WhoAmIConfig();
            RegisterSelfRole();
        }

        public void UpdateConfig(WhoAmIConfig newConfig)
        {
            if (newConfig != null)
            {
                if (newConfig.Distance == null) newConfig.Distance = new DistanceAndKitingConfig();
                if (newConfig.Skills == null) newConfig.Skills = new SpecializedSkillsConfig();
                Config = newConfig;
                RegisterSelfRole();
            }
        }

        private void RegisterSelfRole()
        {
            uint pid = _player?.Id ?? 0;
            if (pid != 0 && Config != null)
            {
                _sharedRolesByPlayerId[pid] = Config.GroupRole;
            }
        }

        private void PublishSupportAlert(uint threatMobId, bool needsRescue)
        {
            uint pid = _player?.Id ?? 0;
            if (pid == 0) return;

            if (!needsRescue)
            {
                _sharedSupportAlerts.TryRemove(pid, out _);
                return;
            }

            _sharedSupportAlerts[pid] = new SupportAlertState
            {
                SupportPlayerId = pid,
                ThreatMobId = threatMobId,
                X = _player.X,
                Y = _player.Y,
                Z = _player.Z,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        private bool TryFindPartySupportAlert(List<PartyMemberInfo> members, out SupportAlertState alert)
        {
            alert = null;
            if (members == null || members.Count == 0) return false;

            var memberIds = new HashSet<uint>();
            foreach (var m in members)
            {
                if (m != null && m.Id != 0 && m.Id != _player.Id)
                    memberIds.Add(m.Id);
            }

            DateTime now = DateTime.UtcNow;
            foreach (var kv in _sharedSupportAlerts)
            {
                if (!memberIds.Contains(kv.Key)) continue;
                var state = kv.Value;
                if (state == null) continue;
                if ((now - state.UpdatedUtc).TotalSeconds > 3.0) continue;

                alert = state;
                return true;
            }

            return false;
        }

        private bool IsCooldownReady(string key, int cooldownMs)
        {
            if (!_cooldowns.TryGetValue(key, out DateTime last))
                return true;
            return (DateTime.UtcNow - last).TotalMilliseconds >= Math.Max(150, cooldownMs);
        }

        private void MarkCooldown(string key)
        {
            _cooldowns[key] = DateTime.UtcNow;
        }

        public bool IsInParty()
        {
            if (_party == null) return false;
            try
            {
                var members = _party.GetMembers();
                return members != null && members.Count > 1;
            }
            catch
            {
                return false;
            }
        }

        public bool IsBossEntity(WorldEntityInfo mob)
        {
            if (mob == null) return false;
            if (Config.AutoDetectBossByNameOrHp)
            {
                if (!string.IsNullOrWhiteSpace(mob.Name))
                {
                    string lower = mob.Name.ToLowerInvariant();
                    if (lower.Contains("boss") || lower.Contains("lord") || lower.Contains("king") || lower.Contains("emperor"))
                        return true;
                }
                if (mob.HPMax >= Config.BossHpThresholdToTreatAsBoss && Config.BossHpThresholdToTreatAsBoss > 0)
                    return true;
            }
            return false;
        }

        public EncounterContext DetectContext(bool isInDungeon, WorldEntityInfo targetMob)
        {
            bool inParty = IsInParty();
            if (!inParty && !isInDungeon)
                return EncounterContext.SoloOpenWorld;

            if (isInDungeon || IsBossEntity(targetMob))
                return EncounterContext.DungeonOrBoss;

            return EncounterContext.GroupOpenWorld;
        }

        private PartyMemberInfo FindTankMember(List<PartyMemberInfo> members)
        {
            if (members == null || members.Count == 0) return null;

            foreach (var m in members)
            {
                if (m != null && m.Id != 0 && _sharedRolesByPlayerId.TryGetValue(m.Id, out var r) && r == WhoAmIRole.Tank)
                    return m;
            }

            return members[0];
        }

        private bool HasRoleInParty(List<PartyMemberInfo> members, WhoAmIRole targetRole)
        {
            if (members == null || members.Count == 0) return false;

            foreach (var m in members)
            {
                if (m == null || m.Id == 0) continue;
                if (m.Id == _player.Id && Config != null && Config.GroupRole == targetRole)
                    return true;
                if (_sharedRolesByPlayerId.TryGetValue(m.Id, out var r) && r == targetRole)
                    return true;
            }

            return false;
        }

        private bool FindNearestMob(out float dist, out float mx, out float my, out float mz, out uint mobId)
        {
            dist = float.MaxValue;
            mx = my = mz = 0f;
            mobId = 0;

            var mobs = _world?.GetMobsAndNpcs();
            if (mobs == null) return false;

            foreach (var m in mobs)
            {
                if (m == null || m.Type != 6 || m.ServerId == 0) continue;
                float d = MoveObstacleAvoidance.Dist2D(_player.X, _player.Z, m.X, m.Z);
                if (d < dist)
                {
                    dist = d;
                    mx = m.X;
                    my = m.Y;
                    mz = m.Z;
                    mobId = m.ServerId;
                }
            }

            return dist < float.MaxValue;
        }

        private bool TryBuildKiteOrDistanceDecision(EncounterContext ctx, WorldEntityInfo targetMob, out WhoAmIDecision decision)
        {
            decision = null;
            if (Config.Distance == null || !Config.Distance.KeepDistanceFromMobs)
                return false;

            float refDist = float.MaxValue;
            float rx = 0f, ry = 0f, rz = 0f;

            if (targetMob != null && targetMob.ServerId != 0)
            {
                refDist = MoveObstacleAvoidance.Dist2D(_player.X, _player.Z, targetMob.X, targetMob.Z);
                rx = targetMob.X;
                ry = targetMob.Y;
                rz = targetMob.Z;
            }
            else if (FindNearestMob(out float nd, out float nx, out float ny, out float nz, out _))
            {
                refDist = nd;
                rx = nx;
                ry = ny;
                rz = nz;
            }

            if (refDist == float.MaxValue)
                return false;

            float minSafe = Math.Max(1.0f, Config.Distance.MinSafeMobDistance);
            if (refDist < minSafe)
            {
                float dx = _player.X - rx;
                float dz = _player.Z - rz;
                float len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len > 0.01f)
                {
                    float step = Math.Max(3.0f, Config.Distance.KiteStepDistance);
                    decision = new WhoAmIDecision
                    {
                        Action = WhoAmIActionType.KiteOrKeepDistance,
                        Context = ctx,
                        DesiredState = BotState.Moving,
                        AllowAttack = false,
                        HasMoveGoal = true,
                        MoveX = _player.X + (dx / len) * step,
                        MoveY = _player.Y,
                        MoveZ = _player.Z + (dz / len) * step,
                        Status = $"Mantendo distância segura do mob ({refDist:F1}m < {minSafe:F1}m)"
                    };
                    return true;
                }
            }

            return false;
        }

        private bool TrySupportActions(EncounterContext ctx, List<PartyMemberInfo> members, WorldEntityInfo targetMob, out WhoAmIDecision decision)
        {
            decision = null;
            var sk = Config.Skills;
            if (sk == null) return false;

            float selfHpPct = _player.HPMax > 0 ? (_player.HP * 100f / _player.HPMax) : 100f;
            bool hasCloseMob = FindNearestMob(out float nearestMobDist, out float mx, out float my, out float mz, out uint nearMobId);
            bool threatClose = hasCloseMob && nearestMobDist <= sk.SupportThreatDistance;

            if (selfHpPct <= sk.SupportFleeHpPercent || (threatClose && nearestMobDist <= Math.Max(4.0f, sk.SupportThreatDistance * 0.6f)))
            {
                PublishSupportAlert(nearMobId, true);

                float fx = _player.X - (hasCloseMob ? (mx - _player.X) : 0f);
                float fz = _player.Z - (hasCloseMob ? (mz - _player.Z) : 0f);
                float len = (float)Math.Sqrt(fx * fx + fz * fz);
                float step = Math.Max(6.0f, Config.Distance?.KiteStepDistance ?? 6.0f);

                decision = new WhoAmIDecision
                {
                    Action = WhoAmIActionType.SupportFlee,
                    Context = ctx,
                    DesiredState = BotState.Moving,
                    AllowAttack = false,
                    SupportNeedsRescue = true,
                    RescueTargetMobId = nearMobId,
                    HasMoveGoal = len > 0.01f,
                    MoveX = len > 0.01f ? _player.X + (fx / len) * step : _player.X,
                    MoveY = _player.Y,
                    MoveZ = len > 0.01f ? _player.Z + (fz / len) * step : _player.Z,
                    Status = $"Suporte fugindo/pedindo socorro (HP {selfHpPct:F0}%, mob {nearestMobDist:F1}m)"
                };
                return true;
            }

            PublishSupportAlert(0, false);

            if (members != null && members.Count > 0)
            {
                if (sk.EnableRevive && sk.ReviveSkillId > 0 && IsCooldownReady("support_revive", sk.ReviveCooldownMs))
                {
                    foreach (var m in members)
                    {
                        if (m != null && m.Id != 0 && m.Id != _player.Id && m.HP == 0)
                        {
                            MarkCooldown("support_revive");
                            decision = new WhoAmIDecision
                            {
                                Action = WhoAmIActionType.CastSpecializedSkill,
                                Context = ctx,
                                DesiredState = BotState.Combat,
                                AllowAttack = false,
                                SpecializedSkillToCast = sk.ReviveSkillId,
                                SpecializedSkillTargetId = m.Id,
                                Status = $"Suporte revivendo membro ID {m.Id}"
                            };
                            return true;
                        }
                    }
                }

                int injuredCount = 0;
                foreach (var m in members)
                {
                    if (m != null && m.Id != 0 && m.HP > 0 && m.HPMax > 0)
                    {
                        float pct = m.HP * 100f / m.HPMax;
                        if (pct <= sk.GroupHealBelowHpPercent)
                            injuredCount++;
                    }
                }

                if (sk.EnableGroupHeal && sk.GroupHealSkillId > 0 && injuredCount >= Math.Max(2, sk.GroupHealMinInjuredMembers) && IsCooldownReady("support_group_heal", 1200))
                {
                    MarkCooldown("support_group_heal");
                    decision = new WhoAmIDecision
                    {
                        Action = WhoAmIActionType.CastSpecializedSkill,
                        Context = ctx,
                        DesiredState = BotState.Combat,
                        AllowAttack = false,
                        SpecializedSkillToCast = sk.GroupHealSkillId,
                        SpecializedSkillTargetId = _player.Id,
                        Status = $"Suporte aplicando cura geral ({injuredCount} membros feridos)"
                    };
                    return true;
                }

                var tank = FindTankMember(members);
                if (sk.EnableSingleHeal && sk.SingleHealSkillId > 0 && IsCooldownReady("support_single_heal", 1000))
                {
                    if (tank != null && tank.Id != 0 && tank.HP > 0 && tank.HPMax > 0)
                    {
                        float tankPct = tank.HP * 100f / tank.HPMax;
                        if (tankPct <= sk.HealTankBelowHpPercent)
                        {
                            MarkCooldown("support_single_heal");
                            decision = new WhoAmIDecision
                            {
                                Action = WhoAmIActionType.CastSpecializedSkill,
                                Context = ctx,
                                DesiredState = BotState.Combat,
                                AllowAttack = false,
                                SpecializedSkillToCast = sk.SingleHealSkillId,
                                SpecializedSkillTargetId = tank.Id,
                                Status = $"Suporte curando Tank ID {tank.Id} ({tankPct:F0}%)"
                            };
                            return true;
                        }
                    }

                    foreach (var m in members)
                    {
                        if (m != null && m.Id != 0 && m.HP > 0 && m.HPMax > 0)
                        {
                            float pct = m.HP * 100f / m.HPMax;
                            if (pct <= sk.HealMemberBelowHpPercent)
                            {
                                MarkCooldown("support_single_heal");
                                decision = new WhoAmIDecision
                                {
                                    Action = WhoAmIActionType.CastSpecializedSkill,
                                    Context = ctx,
                                    DesiredState = BotState.Combat,
                                    AllowAttack = false,
                                    SpecializedSkillToCast = sk.SingleHealSkillId,
                                    SpecializedSkillTargetId = m.Id,
                                    Status = $"Suporte curando membro ID {m.Id} ({pct:F0}%)"
                                };
                                return true;
                            }
                        }
                    }
                }
            }

            if (TryBuildKiteOrDistanceDecision(ctx, targetMob, out var kiteDecision))
            {
                decision = kiteDecision;
                return true;
            }

            return false;
        }

        public WhoAmIDecision Evaluate(
            bool isInDungeon,
            WorldEntityInfo targetMob,
            bool tankIsEngaged,
            bool assistantIsEngaged,
            bool supportInDanger,
            uint supportThreatMobId)
        {
            RegisterSelfRole();

            var ctx = DetectContext(isInDungeon, targetMob);
            var members = _party?.GetMembers() ?? new List<PartyMemberInfo>();

            if (ctx == EncounterContext.SoloOpenWorld || !Config.GroupRoleEnabled)
            {
                if (TryBuildKiteOrDistanceDecision(ctx, targetMob, out var soloKite))
                    return soloKite;

                return new WhoAmIDecision
                {
                    Action = WhoAmIActionType.SoloDefaultRoutine,
                    Context = ctx,
                    DesiredState = BotState.Idle,
                    AllowAttack = true,
                    AllowSoloDefaultRoutine = true,
                    Status = "Rotina solo padrão ativa"
                };
            }

            switch (Config.GroupRole)
            {
                case WhoAmIRole.Support:
                    if (TrySupportActions(ctx, members, targetMob, out var supDec))
                        return supDec;

                    return new WhoAmIDecision
                    {
                        Action = WhoAmIActionType.ReadyToAttack,
                        Context = ctx,
                        DesiredState = BotState.Combat,
                        AllowAttack = targetMob != null,
                        Status = ctx == EncounterContext.DungeonOrBoss
                            ? "[PT: Suporte] DG/Boss: retaguarda e cura ativas"
                            : "[PT: Suporte] Mundo aberto: cura e apoio ativos"
                    };

                case WhoAmIRole.Tank:
                    if (ctx == EncounterContext.DungeonOrBoss && Config.Skills.EnableTankTaunt && Config.Skills.TankTauntSkillId > 0 && targetMob != null)
                    {
                        if (IsCooldownReady("tank_taunt", Config.Skills.TankTauntCooldownMs))
                        {
                            MarkCooldown("tank_taunt");
                            return new WhoAmIDecision
                            {
                                Action = WhoAmIActionType.CastSpecializedSkill,
                                Context = ctx,
                                DesiredState = BotState.Combat,
                                AllowAttack = true,
                                SpecializedSkillToCast = Config.Skills.TankTauntSkillId,
                                SpecializedSkillTargetId = targetMob.ServerId,
                                Status = $"[PT: Tank] Mantendo foco no alvo {targetMob.ServerId}"
                            };
                        }
                    }

                    return new WhoAmIDecision
                    {
                        Action = WhoAmIActionType.AdvanceOrEngage,
                        Context = ctx,
                        DesiredState = targetMob != null ? BotState.Combat : BotState.Moving,
                        AllowAttack = targetMob != null,
                        Status = ctx == EncounterContext.DungeonOrBoss
                            ? "[PT: Tank] Linha de frente em DG/Boss"
                            : "[PT: Tank] Liderando engajamento em grupo"
                    };

                case WhoAmIRole.Assistant:
                    bool hasTankInParty = HasRoleInParty(members, WhoAmIRole.Tank);

                    if (ctx == EncounterContext.DungeonOrBoss && hasTankInParty)
                    {
                        var tank = FindTankMember(members);
                        if (tank != null && tank.Id != 0 && tank.Id != _player.Id)
                        {
                            float dTank = MoveObstacleAvoidance.Dist2D(_player.X, _player.Z, tank.X, tank.Z);
                            if (dTank > Math.Max(2.0f, Config.Skills.AssistantFollowDistance) && !tankIsEngaged)
                            {
                                return new WhoAmIDecision
                                {
                                    Action = WhoAmIActionType.FollowTank,
                                    Context = ctx,
                                    DesiredState = BotState.Following,
                                    AllowAttack = false,
                                    HasMoveGoal = true,
                                    MoveX = tank.X,
                                    MoveY = tank.Y,
                                    MoveZ = tank.Z,
                                    Status = $"[PT: Assistente] Seguindo Tank ({dTank:F1}m)"
                                };
                            }
                        }

                        if (Config.Skills.AssistantWaitForTankEngage && !tankIsEngaged)
                        {
                            return new WhoAmIDecision
                            {
                                Action = WhoAmIActionType.WaitForOrder,
                                Context = ctx,
                                DesiredState = BotState.Idle,
                                AllowAttack = false,
                                Status = "[PT: Assistente] Aguardando Tank engajar no Boss/DG"
                            };
                        }
                    }

                    if (TryBuildKiteOrDistanceDecision(ctx, targetMob, out var assistKite))
                        return assistKite;

                    return new WhoAmIDecision
                    {
                        Action = WhoAmIActionType.ReadyToAttack,
                        Context = ctx,
                        DesiredState = BotState.Combat,
                        AllowAttack = targetMob != null,
                        Status = ctx == EncounterContext.GroupOpenWorld && !hasTankInParty
                            ? "[PT: Assistente] Mundo aberto sem Tank: atuando normalmente em grupo"
                            : "[PT: Assistente] Engajando"
                    };

                case WhoAmIRole.Damage:
                default:
                    bool effectiveDanger = supportInDanger;
                    uint effectiveThreatMob = supportThreatMobId;

                    if (!effectiveDanger && TryFindPartySupportAlert(members, out var sharedAlert))
                    {
                        effectiveDanger = true;
                        effectiveThreatMob = sharedAlert.ThreatMobId;
                    }

                    if (Config.Skills.DamageRescueSupportWhenThreatened && effectiveDanger)
                    {
                        return new WhoAmIDecision
                        {
                            Action = WhoAmIActionType.RescueSupport,
                            Context = ctx,
                            DesiredState = BotState.Combat,
                            AllowAttack = true,
                            RescueTargetMobId = effectiveThreatMob,
                            Status = $"[PT: Dano] Socorrendo Suporte sob ameaça (Mob ID {effectiveThreatMob})"
                        };
                    }

                    bool hasTankOrAssistant = HasRoleInParty(members, WhoAmIRole.Tank) || HasRoleInParty(members, WhoAmIRole.Assistant);
                    if (ctx == EncounterContext.DungeonOrBoss && hasTankOrAssistant && Config.Skills.DamageWaitForAssistantOrTank)
                    {
                        if (!tankIsEngaged || !assistantIsEngaged)
                        {
                            return new WhoAmIDecision
                            {
                                Action = WhoAmIActionType.WaitForOrder,
                                Context = ctx,
                                DesiredState = BotState.Idle,
                                AllowAttack = false,
                                Status = !tankIsEngaged
                                    ? "[PT: Dano] Aguardando Tank iniciar"
                                    : "[PT: Dano] Aguardando Assistente iniciar após o Tank"
                            };
                        }
                    }

                    if (TryBuildKiteOrDistanceDecision(ctx, targetMob, out var dmgKite))
                        return dmgKite;

                    return new WhoAmIDecision
                    {
                        Action = WhoAmIActionType.ReadyToAttack,
                        Context = ctx,
                        DesiredState = BotState.Combat,
                        AllowAttack = targetMob != null,
                        Status = ctx == EncounterContext.GroupOpenWorld && !hasTankOrAssistant
                            ? "[PT: Dano] Mundo aberto sem Tank/Assistente: atacando normalmente"
                            : "[PT: Dano] Atacando"
                    };
            }
        }
    }
}