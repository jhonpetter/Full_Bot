using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public partial class BotCombat
    {
        private readonly PlayerCore _player;
        private readonly PlayerSkills _skills;
        private readonly WorldEntities _world;

        public BotCombat(PlayerCore player, PlayerSkills skills, WorldEntities world)
        {
            _player = player;
            _skills = skills;
            _world = world;
        }

        public WorldEntityInfo FindNearestTarget(IEnumerable<uint> allowedTemplateIds, float maxDistance = 40f)
        {
            var idSet = allowedTemplateIds != null
                ? new HashSet<uint>(allowedTemplateIds)
                : new HashSet<uint>();

            return _world.GetMobsAndNpcs()
                .Where(m => m.HP > 0 && (idSet.Count == 0 || idSet.Contains(m.TemplateId)) && m.Distance <= maxDistance)
                .OrderBy(m => m.Distance)
                .FirstOrDefault();
        }

        public bool AttackTarget(uint targetServerId, IEnumerable<uint> preferredSkillIds = null)
        {
            if (targetServerId == 0) return false;

            if (_player.TargetId != targetServerId)
            {
                _player.TargetId = targetServerId;
                return true;
            }

            var learned = _skills.GetLearnedSkills();
            if (preferredSkillIds != null)
            {
                foreach (uint skillId in preferredSkillIds)
                {
                    var match = learned.FirstOrDefault(s => s.Id == skillId && s.IsReady);
                    if (match != null)
                    {
                        _player.CastSkill(match.Id);
                        return true;
                    }
                }
            }

            _player.AutoAttack();
            return true;
        }
    }
}