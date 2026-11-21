using System;
using System.Collections.Generic;

namespace Full_Bot
{
    public enum SkillRotationMode
    {
        MacroLoop = 0,
        PriorityReady = 1,
        SequentialStep = 2
    }

    public class CombatSkillMacroConfig
    {
        public bool Enabled { get; set; } = true;
        public SkillRotationMode Mode { get; set; } = SkillRotationMode.MacroLoop;
        public double SkillCastIntervalSeconds { get; set; } = 0.65;
        public bool FallbackToAutoAttackWhenNoSkillReady { get; set; } = true;
        public List<uint> SkillIds { get; set; } = new List<uint>();
    }

    public class CombatSkillMacro
    {
        private readonly PlayerCore _player;
        private readonly PlayerSkills _skills;
        private readonly CombatSkillMacroConfig _config;
        private readonly CombatDistanceProfile _distanceProfile;

        private int _cursorIndex = 0;
        private DateTime _lastActionUtc = DateTime.MinValue;

        public CombatSkillMacro(
            PlayerCore player,
            PlayerSkills skills,
            CombatSkillMacroConfig config,
            CombatDistanceProfile distanceProfile)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _skills = skills ?? throw new ArgumentNullException(nameof(skills));
            _config = config ?? new CombatSkillMacroConfig();
            if (_config.SkillIds == null)
                _config.SkillIds = new List<uint>();
            _distanceProfile = distanceProfile ?? CombatDistanceProfile.CreatePreset(CombatArchetype.Fisico);
        }

        public float GetSkillMaxRange()
        {
            return _distanceProfile.ResolveEffectiveMaxRange();
        }

        public float GetApproachStopDistance()
        {
            return _distanceProfile.ResolveApproachDistance();
        }

        public bool IsWithinSkillRange(float mobDistance)
        {
            if (float.IsNaN(mobDistance) || float.IsInfinity(mobDistance))
                return false;

            float maxRange = GetSkillMaxRange();
            return mobDistance <= maxRange;
        }

        public bool TryExecuteAttackOrMacro(
            float mobDistance,
            out bool needsApproach,
            out string status)
        {
            status = string.Empty;
            needsApproach = false;

            if (_player.Idle != 0)
                return false;

            bool hasSkills = _config.Enabled && _config.SkillIds != null && _config.SkillIds.Count > 0;
            if (hasSkills && !IsWithinSkillRange(mobDistance))
            {
                needsApproach = true;
                status = $"Mob fora do alcance da skill ({mobDistance:F1}m > {GetSkillMaxRange():F1}m) -> aproximando.";
                return false;
            }

            DateTime now = DateTime.UtcNow;
            if ((now - _lastActionUtc).TotalSeconds < Math.Max(0.15, _config.SkillCastIntervalSeconds))
                return false;

            if (hasSkills)
            {
                uint chosenSkill = PickSkill();
                if (chosenSkill != 0)
                {
                    _lastActionUtc = now;
                    _player.CastSkill(chosenSkill);
                    status = $"Skill/Macro ID {chosenSkill} (Dist: {mobDistance:F1}m / Limite: {GetSkillMaxRange():F1}m)";
                    return true;
                }
            }

            if (_config.FallbackToAutoAttackWhenNoSkillReady)
            {
                _lastActionUtc = now;
                _player.AutoAttack();
                status = $"AutoAttack (Dist: {mobDistance:F1}m)";
                return true;
            }

            return false;
        }

        private uint PickSkill()
        {
            int count = _config.SkillIds.Count;
            if (count == 0) return 0;

            if (_config.Mode == SkillRotationMode.PriorityReady)
            {
                for (int i = 0; i < count; i++)
                {
                    uint sid = _config.SkillIds[i];
                    if (sid != 0 && (_skills == null || _skills.IsSkillReady(sid)))
                        return sid;
                }
                return 0;
            }

            for (int attempts = 0; attempts < count; attempts++)
            {
                int idx = (_cursorIndex + attempts) % count;
                uint sid = _config.SkillIds[idx];
                if (sid == 0) continue;

                if (_skills == null || _skills.IsSkillReady(sid))
                {
                    _cursorIndex = (idx + 1) % count;
                    return sid;
                }
            }

            if (_config.Mode == SkillRotationMode.MacroLoop)
            {
                uint fallbackId = _config.SkillIds[_cursorIndex % count];
                _cursorIndex = (_cursorIndex + 1) % count;
                return fallbackId;
            }

            return 0;
        }
    }
}