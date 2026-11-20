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

        public CombatModule(PlayerCore player, PlayerSkills skills, WorldEntities world)
        {
            _player = player;
            _skills = skills;
            _world = world;
        }

        public bool TryFarmMobs(List<uint> targetTemplateIds, float centerX, float centerY, float centerZ, out string status)
        {
            status = string.Empty;
            if (targetTemplateIds == null || targetTemplateIds.Count == 0) return false;

            // Se estiver longe do ponto de farm e sem alvo, vai até a área
            float distArea = SupportModule.Distance2D(_player.X, _player.Z, centerX, centerZ);
            if (distArea > 45f && _player.TargetId == 0)
            {
                _player.MoveTo(centerX, centerY, centerZ);
                status = $"Indo para área de farm ({distArea:F0}m)";
                return true;
            }

            // Busca mob vivo mais próximo pelo TemplateId
            var mob = _world.GetMobsAndNpcs()
                .Where(m => m.Type == 6 && m.HP > 0 && targetTemplateIds.Contains(m.TemplateId))
                .OrderBy(m => m.Distance)
                .FirstOrDefault();

            if (mob == null) return false;

            if (_player.TargetId != mob.ServerId)
            {
                _player.TargetId = mob.ServerId;
                status = $"Selecionando {mob.Name} (ID: {mob.TemplateId})";
                return true;
            }

            ExecuteAttackRotation();
            status = $"Atacando {mob.Name} ({mob.HP}/{mob.HPMax})";
            return true;
        }

        public bool TryHelpOtherBots(out string status)
        {
            status = string.Empty;
            if (!HelpAlliesInQuest) return false;

            // Procura outro bot aberto que esteja em combate com um alvo ativo
            var ally = BotHub.GetSnapshot().FirstOrDefault(b =>
                b.Player.Id != _player.Id &&
                b.CurrentState == BotState.Combatendo &&
                b.Player.TargetId != 0);

            if (ally == null) return false;

            float dist = SupportModule.Distance2D(_player.X, _player.Z, ally.Player.X, ally.Player.Z);
            if (dist > 25f)
            {
                _player.MoveTo(ally.Player.X, ally.Player.Y, ally.Player.Z);
                status = $"Indo ajudar {ally.Player.Name} ({dist:F0}m)";
                return true;
            }

            if (_player.TargetId != ally.Player.TargetId)
            {
                _player.TargetId = ally.Player.TargetId;
                status = $"Assistindo alvo de {ally.Player.Name}";
                return true;
            }

            ExecuteAttackRotation();
            status = $"Ajudando {ally.Player.Name} no combate";
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
