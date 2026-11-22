using System;

namespace Full_Bot
{
    public class BotModulesDecision
    {
        public bool Handled { get; set; }
        public BotState DesiredState { get; set; } = BotState.Idle;
        public string Status { get; set; } = string.Empty;

        public bool HasMoveGoal { get; set; }
        public float MoveX { get; set; }
        public float MoveY { get; set; }
        public float MoveZ { get; set; }
        public bool UseFlight { get; set; }

        public bool AllowAttack { get; set; } = true;
        public uint OverrideTargetServerId { get; set; }
        public uint SpecializedSkillId { get; set; }
        public uint SpecializedSkillTargetId { get; set; }
    }

    public class BotEngineModules
    {
        private readonly PlayerCore _player;
        private readonly PlayerParty _party;
        private readonly WorldEntities _world;
        private readonly MemoryProcess _mem;
        private readonly Move _move;

        private string _currentCharKey = "default";
        private DateTime _lastConfigReloadUtc = DateTime.MinValue;

        public WhoAmI WhoAmI { get; private set; }
        public SelfCultivation Cultivation { get; private set; }
        public Combat Combat { get; private set; }

        public BotEngineModules(
            PlayerCore player,
            PlayerParty party,
            PlayerBuffs buffs,
            PlayerSkills skills,
            WorldEntities world,
            MemoryProcess mem = null,
            Move move = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _party = party;
            _world = world;
            _mem = mem;
            _move = move ?? new Move(_player, _world);

            var whoCfg = WhoAmIConfig.LoadForChar(_currentCharKey);
            var cultCfg = SelfCultivationConfig.LoadForChar(_currentCharKey);
            var buffCfg = CombatBuffConfig.LoadForChar(_currentCharKey);

            WhoAmI = new WhoAmI(_player, _party, _world, whoCfg);
            Cultivation = new SelfCultivation(_player, _party, _world, cultCfg);
            Combat = new Combat(_player, buffs, skills, _party, _world, _mem, buffCfg);
            ApplyBuffConfigCombatSettings(buffCfg);
            _lastConfigReloadUtc = DateTime.UtcNow;
        }

        private void ApplyBuffConfigCombatSettings(CombatBuffConfig buffCfg)
        {
            if (Combat == null || buffCfg == null) return;

            var distProfile = CombatDistanceProfile.CreatePreset(buffCfg.Archetype);
            var macroCfg = new CombatSkillMacroConfig
            {
                Enabled = Combat.SkillMacro != null ? true : true,
                CasterKind = buffCfg.CasterKind
            };

            Combat.UpdateCombatProfile(distProfile, macroCfg);
        }

        public void LoadForCharacter(string charName, bool force = false)
        {
            string normalizedKey = string.IsNullOrWhiteSpace(charName) ? "default" : charName;
            DateTime now = DateTime.UtcNow;
            bool charChanged = !string.Equals(_currentCharKey, normalizedKey, StringComparison.Ordinal);

            if (!force && !charChanged && (now - _lastConfigReloadUtc).TotalSeconds < 2.0)
                return;

            _currentCharKey = normalizedKey;
            _lastConfigReloadUtc = now;

            var whoCfg = WhoAmIConfig.LoadForChar(_currentCharKey);
            var cultCfg = SelfCultivationConfig.LoadForChar(_currentCharKey);
            var buffCfg = CombatBuffConfig.LoadForChar(_currentCharKey);

            WhoAmI?.UpdateConfig(whoCfg);
            Cultivation?.UpdateConfig(cultCfg);
            Combat?.UpdateBuffConfig(buffCfg);
            ApplyBuffConfigCombatSettings(buffCfg);
        }

        public bool ExecuteMovement(bool useFlight, float x, float y, float z, out BotState state, out string status)
        {
            state = BotState.Moving;
            status = string.Empty;

            if (useFlight)
            {
                int alt = (int)Math.Max(1f, Math.Min(69f, Math.Abs(y)));
                _player.MoveToFly(x, z, alt);
                status = $"Voando para ({x:F0}, {z:F0}) alt {alt}m";
                return true;
            }

            if (_move != null)
            {
                return _move.GoTo(x, y, z, out state, out status);
            }

            _player.MoveTo(x, y, z);
            status = $"Movendo para ({x:F0}, {y:F0}, {z:F0})";
            return true;
        }

        public BotModulesDecision EvaluateStep(
            bool isInDungeon,
            WorldEntityInfo targetMob,
            bool isCurrentlyKillingMob,
            bool tankIsEngaged,
            bool assistantIsEngaged,
            bool supportInDanger,
            uint supportThreatMobId)
        {
            var result = new BotModulesDecision
            {
                DesiredState = BotState.Idle,
                AllowAttack = true
            };

            if (WhoAmI != null && WhoAmI.Config != null)
            {
                var whoDec = WhoAmI.Evaluate(
                    isInDungeon,
                    targetMob,
                    tankIsEngaged,
                    assistantIsEngaged,
                    supportInDanger,
                    supportThreatMobId);

                result.AllowAttack = whoDec.AllowAttack;
                result.SpecializedSkillId = whoDec.SpecializedSkillToCast;
                result.SpecializedSkillTargetId = whoDec.SpecializedSkillTargetId;
                result.OverrideTargetServerId = whoDec.RescueTargetMobId;

                if (whoDec.Action == WhoAmIActionType.SupportFlee ||
                    whoDec.Action == WhoAmIActionType.KiteOrKeepDistance ||
                    whoDec.Action == WhoAmIActionType.FollowTank ||
                    whoDec.Action == WhoAmIActionType.WaitForOrder ||
                    whoDec.Action == WhoAmIActionType.CastSpecializedSkill)
                {
                    result.Handled = true;
                    result.DesiredState = whoDec.DesiredState;
                    result.HasMoveGoal = whoDec.HasMoveGoal;
                    result.MoveX = whoDec.MoveX;
                    result.MoveY = whoDec.MoveY;
                    result.MoveZ = whoDec.MoveZ;
                    result.Status = whoDec.Status;
                    return result;
                }
            }

            if (Cultivation != null && Cultivation.Config != null && Cultivation.Config.Enabled)
            {
                var cultDec = Cultivation.Step();
                if (cultDec != null && cultDec.Handled)
                {
                    result.Handled = true;
                    result.DesiredState = cultDec.DesiredState;
                    result.HasMoveGoal = cultDec.HasMoveGoal;
                    result.MoveX = cultDec.MoveX;
                    result.MoveY = cultDec.MoveY;
                    result.MoveZ = cultDec.MoveZ;
                    result.UseFlight = cultDec.UseFlight;
                    result.OverrideTargetServerId = cultDec.TargetMobId;
                    result.Status = cultDec.Status;
                    return result;
                }
            }

            if (Combat != null && result.AllowAttack)
            {
                var stepMob = targetMob;
                if (stepMob == null && result.OverrideTargetServerId != 0 && _world != null)
                {
                    var mobs = _world.GetMobsAndNpcs();
                    if (mobs != null)
                    {
                        for (int i = 0; i < mobs.Count; i++)
                        {
                            if (mobs[i] != null && mobs[i].ServerId == result.OverrideTargetServerId)
                            {
                                stepMob = mobs[i];
                                break;
                            }
                        }
                    }
                }

                if (stepMob != null)
                {
                    var combatStep = Combat.StepCombat(stepMob, isCurrentlyKillingMob);
                    if (combatStep != null && combatStep.Handled)
                    {
                        result.Handled = true;
                        result.DesiredState = combatStep.DesiredState;
                        result.Status = combatStep.Status;
                        return result;
                    }
                }
            }

            return result;
        }
    }
}