using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Full_Bot
{
    public enum CharacterCastMode
    {
        Universal = 0, // Pode usar ataque físico e magia
        MagicOnly = 1  // Só pode usar magia
    }

    public class CastSkill
    {
        private const string ScriptsFolder = "Scripts";
        private const string ScriptFileName = "CastSkill.asm";

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        public CharacterCastMode CharacterMode { get; set; } = CharacterCastMode.Universal;

        public CastSkill(MemoryProcess mem, AsmRunner asm, CharacterCastMode mode = CharacterCastMode.Universal)
        {
            _mem = mem ?? throw new ArgumentNullException(nameof(mem));
            _asm = asm ?? throw new ArgumentNullException(nameof(asm));
            CharacterMode = mode;
        }

        private static string ResolveScriptPath()
        {
            string baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ScriptsFolder, ScriptFileName);
            if (File.Exists(baseDirPath))
                return baseDirPath;

            return Path.Combine(ScriptsFolder, ScriptFileName);
        }

        public SkillInfo GetSkillState(PlayerCore player, uint skillId)
        {
            if (player == null || skillId == 0u)
                return null;

            return player.GetSkills().FirstOrDefault(s => s != null && s.Id == skillId);
        }

        public bool IsSkillReady(PlayerCore player, uint skillId)
        {
            if (player == null || skillId == 0u)
                return false;

            var skill = GetSkillState(player, skillId);
            if (skill == null)
                return true;

            return skill.Cooldown == 0u;
        }

        public bool IsTargetWithinSkillRange(PlayerCore player, SkillInfo skill, float? targetDistance = null)
        {
            if (player == null || skill == null)
                return true;

            // Se a skill é exclusivamente em si mesmo, não exige distância de alvo externo
            if (skill.RequiresSelfOnly)
                return true;

            float range = skill.Range;
            if (range <= 0f)
                return true;

            float dist = targetDistance ?? GetCurrentTargetDistance(player);
            if (dist < 0f)
                return false;

            return dist <= range;
        }

        private float GetCurrentTargetDistance(PlayerCore player)
        {
            // Tenta obter distância do alvo atual via nós/propriedades disponíveis no PlayerCore / MemoryProcess
            uint pBase = player.Base;
            if (!MemoryProcess.IsValidPointer(pBase))
                return -1f;

            uint targetId = _mem.ReadUIntFromNodeAtBase(pBase, "target_id");
            if (targetId == 0u)
                return -1f;

            float dist = _mem.ReadFloatFromNodeAtBase(pBase, "target_distance");
            if (dist > 0f && !float.IsNaN(dist) && !float.IsInfinity(dist))
                return dist;

            return -1f;
        }

        public bool TryCast(
            PlayerCore player,
            uint skillId,
            bool requireSkillInList = false,
            float? targetDistance = null,
            CharacterCastMode? modeOverride = null)
        {
            if (skillId == 0u)
                return false;

            CharacterCastMode activeMode = modeOverride ?? CharacterMode;

            if (player != null)
            {
                var skill = GetSkillState(player, skillId);
                if (skill == null)
                {
                    if (requireSkillInList)
                        return false;
                }
                else
                {
                    if (skill.Cooldown != 0u)
                        return false;

                    // Restrição de personagem que só pode usar magia
                    if (activeMode == CharacterCastMode.MagicOnly &&
                        skill.Category == SkillCategory.Attack &&
                        !skill.IsMagic)
                    {
                        return false;
                    }

                    // Validação de alvo para buffs de grupo e checagem de distância para qualquer skill/buff não-SelfOnly
                    if (!skill.RequiresSelfOnly)
                    {
                        if (skill.TargetScope == SkillTargetScope.PartyMember && !player.IsInParty())
                            return false;

                        if (!IsTargetWithinSkillRange(player, skill, targetDistance))
                            return false;
                    }
                }
            }

            var vars = new Dictionary<string, uint>
            {
                { "SKILL_ID", skillId }
            };

            _asm.RunScript(_mem, ResolveScriptPath(), vars);
            return true;
        }
    }

    public partial class PlayerCore
    {
        private CastSkill _castSkill;

        public CastSkill CastSkillModule
        {
            get
            {
                if (_castSkill == null)
                    _castSkill = new CastSkill(_mem, _asm);
                return _castSkill;
            }
        }

        public CharacterCastMode CastMode
        {
            get => CastSkillModule.CharacterMode;
            set => CastSkillModule.CharacterMode = value;
        }

        public SkillInfo GetSkillInfo(uint skillId)
        {
            return CastSkillModule.GetSkillState(this, skillId);
        }

        public float GetSkillRange(uint skillId)
        {
            return GetSkillInfo(skillId)?.Range ?? 0f;
        }

        public bool CanUseSkill(uint skillId)
        {
            return CastSkillModule.IsSkillReady(this, skillId);
        }

        public bool UseSkillOrBuff(
            uint skillId,
            bool requireSkillInList = false,
            float? targetDistance = null,
            CharacterCastMode? modeOverride = null)
        {
            return CastSkillModule.TryCast(this, skillId, requireSkillInList, targetDistance, modeOverride);
        }
    }
}