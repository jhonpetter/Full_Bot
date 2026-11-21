using System;

namespace Full_Bot
{
    public enum CombatActionKind
    {
        None = 0,
        BuffOrGroupSync = 1,
        TargetInvalidOrOutsidePlayer = 2,
        ApproachRequired = 3,
        AttackOrSkillExecuted = 4
    }

    public class CombatStepResult
    {
        public bool Handled { get; set; }
        public CombatActionKind Action { get; set; } = CombatActionKind.None;
        public BotState DesiredState { get; set; } = BotState.Idle;
        public bool NeedsApproach { get; set; }
        public float ApproachDistance { get; set; }
        public uint MobCurrentTargetId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class Combat
    {
        private readonly PlayerCore _player;
        private readonly PlayerBuffs _buffs;
        private readonly PlayerSkills _skills;
        private readonly PlayerParty _party;
        private readonly WorldEntities _world;
        private readonly MemoryProcess _mem;

        private CombatBuffConfig _buffConfig;
        private CombatBuffManager _buffManager;
        private CombatDistanceProfile _distanceProfile;
        private CombatSkillMacroConfig _skillMacroConfig;
        private CombatSkillMacro _skillMacro;
        private CombatTargetValidator _targetValidator;

        public CombatBuffConfig BuffConfig => _buffConfig;
        public CombatBuffManager BuffManager => _buffManager;
        public CombatDistanceProfile DistanceProfile => _distanceProfile;
        public CombatSkillMacro SkillMacro => _skillMacro;
        public CombatTargetValidator TargetValidator => _targetValidator;

        public Combat(
            PlayerCore player,
            PlayerBuffs buffs,
            PlayerSkills skills,
            PlayerParty party,
            WorldEntities world,
            MemoryProcess mem = null,
            CombatBuffConfig buffConfig = null,
            CombatDistanceProfile distanceProfile = null,
            CombatSkillMacroConfig skillMacroConfig = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _buffs = buffs;
            _skills = skills;
            _party = party;
            _world = world;
            _mem = mem;

            _buffConfig = buffConfig ?? new CombatBuffConfig();
            _distanceProfile = distanceProfile ?? CombatDistanceProfile.CreatePreset(CombatArchetype.Fisico);
            _skillMacroConfig = skillMacroConfig ?? new CombatSkillMacroConfig();

            RebuildSubmodules();
        }

        private void RebuildSubmodules()
        {
            _buffManager = new CombatBuffManager(_player, _buffs, _party, _world, _buffConfig);
            _skillMacro = new CombatSkillMacro(_player, _skills, _skillMacroConfig, _distanceProfile);
            _targetValidator = new CombatTargetValidator(_player, _party, _mem);
        }

        public void UpdateBuffConfig(CombatBuffConfig newConfig)
        {
            if (newConfig == null) return;
            _buffConfig = newConfig;
            _buffManager = new CombatBuffManager(_player, _buffs, _party, _world, _buffConfig);
        }

        public void UpdateCombatProfile(CombatDistanceProfile profile, CombatSkillMacroConfig macroConfig = null)
        {
            if (profile != null)
                _distanceProfile = profile;
            if (macroConfig != null)
                _skillMacroConfig = macroConfig;

            _skillMacro = new CombatSkillMacro(_player, _skills, _skillMacroConfig, _distanceProfile);
        }

        public bool TryProcessBuffsOnly(bool isCurrentlyKillingMob, out BotState desiredState, out string status)
        {
            return _buffManager.TryProcessBuffs(isCurrentlyKillingMob, out desiredState, out status);
        }

        public CombatStepResult StepCombat(WorldEntityInfo targetMob, bool isCurrentlyKillingMob)
        {
            var result = new CombatStepResult
            {
                ApproachDistance = _skillMacro.GetApproachStopDistance()
            };

            // 1. Avalia buffs / reunião de grupo antes de engajar (conforme regras de solo/grupo)
            if (_buffManager.TryProcessBuffs(isCurrentlyKillingMob, out BotState buffState, out string buffStatus))
            {
                result.Handled = true;
                result.Action = CombatActionKind.BuffOrGroupSync;
                result.DesiredState = buffState;
                result.Status = buffStatus;
                return result;
            }

            // 2. Valida se o alvo existe e não pertence a jogador de fora da PT
            if (_targetValidator.IsMobEngagedByOutsidePlayer(targetMob, out uint mobTargetId, out string rejectReason))
            {
                result.Handled = true;
                result.Action = CombatActionKind.TargetInvalidOrOutsidePlayer;
                result.MobCurrentTargetId = mobTargetId;
                result.DesiredState = BotState.Idle;
                result.Status = rejectReason;
                return result;
            }

            result.MobCurrentTargetId = mobTargetId;

            // 3. Garante seleção do alvo e executa skill/macro ou sinaliza aproximação
            if (_player.TargetId != targetMob.ServerId)
            {
                _player.SelectTarget(targetMob.ServerId);
            }

            float dist = targetMob.Distance;
            if (dist <= 0.001f && _world != null)
            {
                dist = MoveObstacleAvoidance.Dist2D(_player.X, _player.Z, targetMob.X, targetMob.Z);
            }

            bool executed = _skillMacro.TryExecuteAttackOrMacro(dist, out bool needsApproach, out string attackStatus);
            if (needsApproach)
            {
                result.Handled = false;
                result.Action = CombatActionKind.ApproachRequired;
                result.NeedsApproach = true;
                result.ApproachDistance = _skillMacro.GetApproachStopDistance();
                result.Status = attackStatus;
                return result;
            }

            if (executed)
            {
                result.Handled = true;
                result.Action = CombatActionKind.AttackOrSkillExecuted;
                result.DesiredState = BotState.Combat;
                result.Status = attackStatus;
                return result;
            }

            result.Handled = false;
            result.Status = attackStatus;
            return result;
        }
    }
}