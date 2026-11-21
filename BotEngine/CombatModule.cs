using System;
using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public class CombatModule
    {
        private readonly PlayerCore _player;
        private readonly PlayerSkills _skills;
        private readonly WorldEntities _world;

        public List<uint> AttackSkillRotation { get; } = new List<uint>();
        public List<uint> FarmMobTemplateIds { get; } = new List<uint>();
        public bool HelpAlliesInQuest { get; set; } = true;

        public float FarmCenterX { get; set; }
        public float FarmCenterY { get; set; }
        public float FarmCenterZ { get; set; }

        public CombatModule(PlayerCore player, PlayerSkills skills, WorldEntities world)
        {
            _player = player;
            _skills = skills;
            _world = world;
        }

        private static bool IsValidFloat(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private float GetEffectiveDistance2D(WorldEntityInfo entity)
        {
            if (entity == null)
                return float.MaxValue;

            float px = _player.X;
            float pz = _player.Z;
            float ex = entity.X;
            float ez = entity.Z;

            if (IsValidFloat(px) && IsValidFloat(pz) && IsValidFloat(ex) && IsValidFloat(ez))
            {
                float coordDist = SupportModule.Distance2D(px, pz, ex, ez);
                if (IsValidFloat(coordDist) && coordDist >= 0f)
                {
                    if (coordDist > 0.01f)
                        return coordDist;

                    if (IsValidFloat(entity.Distance) && entity.Distance > 0f)
                        return entity.Distance;

                    return coordDist;
                }
            }

            if (IsValidFloat(entity.Distance) && entity.Distance >= 0f)
                return entity.Distance;

            return float.MaxValue;
        }

        public string BuildTargetDiagnostic()
        {
            if (_player == null || _world == null)
                return "TargetLog: sem player/world";

            uint currentTargetId = _player.TargetId;
            var entities = (_world.GetMobsAndNpcs() ?? new List<WorldEntityInfo>()).ToList();
            var currentTarget = currentTargetId != 0
                ? entities.FirstOrDefault(e => e != null && e.ServerId == currentTargetId)
                : null;

            string targetPart = currentTarget != null
                ? $"Target: {currentTarget.Name} | ID={currentTarget.TemplateId} | SrvID={currentTarget.ServerId} ({PlayerCore.FormatHex32(currentTarget.ServerId)}) | Type={currentTarget.Type} | HP={currentTarget.HP}/{currentTarget.HPMax} | Dist={GetEffectiveDistance2D(currentTarget):F1}m"
                : $"Target: {(currentTargetId == 0 ? "0 (nenhum)" : $"{currentTargetId} ({PlayerCore.FormatHex32(currentTargetId)} - não listado)")}";

            var idSet = new HashSet<uint>(FarmMobTemplateIds);
            var nearestMob = entities
                .Where(m => m != null && m.Type == 6 && m.ServerId != 0 && (idSet.Count == 0 || idSet.Contains(m.TemplateId)))
                .OrderBy(m => GetEffectiveDistance2D(m))
                .FirstOrDefault();

            if (nearestMob == null)
                return $"{targetPart} || Cand: nenhum mob Type=6";

            return $"{targetPart} || Cand: {nearestMob.Name} | ID={nearestMob.TemplateId} | SrvID={nearestMob.ServerId} ({PlayerCore.FormatHex32(nearestMob.ServerId)}) | Type={nearestMob.Type} | HP={nearestMob.HP}/{nearestMob.HPMax} | Dist={GetEffectiveDistance2D(nearestMob):F1}m";
        }

        public bool TryFarmMobs(List<uint> targetTemplateIds, float centerX, float centerY, float centerZ, out string status)
        {
            status = string.Empty;
            if (targetTemplateIds == null || targetTemplateIds.Count == 0) return false;

            float distArea = SupportModule.Distance2D(_player.X, _player.Z, centerX, centerZ);
            if (distArea > 45f && _player.TargetId == 0)
            {
                _player.MoveTo(centerX, centerY, centerZ);
                status = $"Indo para área de farm ({distArea:F0}m)";
                return true;
            }

            var idSet = new HashSet<uint>(targetTemplateIds);
            var rawEntities = (_world.GetMobsAndNpcs() ?? new List<WorldEntityInfo>()).ToList();

            // HP do mob não selecionado costuma vir 0 na memória antes do target; não filtrar por HP == 0
            var filtered = new List<WorldEntityInfo>();
            foreach (var m in rawEntities)
            {
                if (m == null) continue;
                if (m.Type != 6) continue;
                if (m.ServerId == 0) continue;
                if (!idSet.Contains(m.TemplateId)) continue;
                filtered.Add(m);
            }

            if (filtered.Count == 0)
            {
                status = $"Nenhum mob Type=6 com TemplateId [{string.Join(", ", idSet)}] encontrado (total lidos: {rawEntities.Count}).";
                return false;
            }

            var mob = filtered
                .OrderBy(m => GetEffectiveDistance2D(m))
                .FirstOrDefault();

            if (mob == null)
            {
                status = $"Falha ao ordenar {filtered.Count} candidatos encontrados.";
                return false;
            }

            float mobDist = GetEffectiveDistance2D(mob);

            if (mobDist > 38f && IsValidFloat(mob.X) && IsValidFloat(mob.Y) && IsValidFloat(mob.Z))
            {
                _player.MoveTo(mob.X, mob.Y, mob.Z);
                status = $"Aproximando de {mob.Name} (TemplateID: {mob.TemplateId} | ServerID: {mob.ServerId} / {PlayerCore.FormatHex32(mob.ServerId)} | {mobDist:F0}m)";
                return true;
            }

            // 1. Seleciona pelo ServerId da instância (SelectTarget) e em seguida ataca (AutoAttack / Skill)
            _player.SelectTarget(mob.ServerId);
            ExecuteAttackRotation();

            status = $"SelectTarget -> Attack: {mob.Name} (TemplateID: {mob.TemplateId} | ServerID: {mob.ServerId} / {PlayerCore.FormatHex32(mob.ServerId)}) HP={mob.HP}/{mob.HPMax}";
            return true;
        }

        public bool TryHelpOtherBots(out string status)
        {
            status = string.Empty;
            if (!HelpAlliesInQuest) return false;

            var ally = BotHub.GetSnapshot().FirstOrDefault(b =>
                b != null &&
                b.Player != null &&
                b.Player.Id != _player.Id &&
                b.CurrentState == BotState.Combat &&
                b.Player.TargetId != 0);

            if (ally == null) return false;

            float dist = SupportModule.Distance2D(_player.X, _player.Z, ally.Player.X, ally.Player.Z);
            if (dist > 25f)
            {
                _player.MoveTo(ally.Player.X, ally.Player.Y, ally.Player.Z);
                status = $"Indo ajudar {ally.Player.Name} ({dist:F0}m)";
                return true;
            }

            _player.SelectTarget(ally.Player.TargetId);
            ExecuteAttackRotation();

            status = $"Ajudando {ally.Player.Name} no combate (ServerID: {ally.Player.TargetId} / {PlayerCore.FormatHex32(ally.Player.TargetId)})";
            return true;
        }

        public void ExecuteAttackRotation()
        {
            if (_player.Idle != 0) return;

            foreach (uint skillId in AttackSkillRotation)
            {
                if (_skills.IsSkillReady(skillId))
                {
                    _player.CastSkill(skillId);
                    return;
                }
            }
            _player.AutoAttack();
        }
    }
}