using System;
using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public class MoveObstacleAvoidance
    {
        private readonly PlayerCore _player;
        private readonly WorldEntities _world;
        private readonly MoveConfig _config;

        public MoveObstacleAvoidance(PlayerCore player, WorldEntities world, MoveConfig config)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void ComputeAvoidedDestination(
            float goalX,
            float goalZ,
            uint ignoreServerId,
            HashSet<uint> ignoreTemplateIds,
            out float outX,
            out float outZ,
            out string avoidReason)
        {
            outX = goalX;
            outZ = goalZ;
            avoidReason = string.Empty;

            // Só desvia no chão (MoveMode == 0)
            if (!_config.AvoidMobsOnGround || _player.MoveMode != 0)
                return;

            float px = _player.X;
            float pz = _player.Z;
            float dx = goalX - px;
            float dz = goalZ - pz;
            float goalDist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (goalDist < 0.01f)
                return;

            float dirX = dx / goalDist;
            float dirZ = dz / goalDist;

            var entities = _world?.GetMobsAndNpcs();
            if (entities == null)
                return;

            var blockingMobs = entities
                .Where(m =>
                    m != null &&
                    m.Type == 6 &&
                    m.ServerId != 0 &&
                    m.ServerId != ignoreServerId &&
                    (ignoreTemplateIds == null || !ignoreTemplateIds.Contains(m.TemplateId)))
                .Select(m => new
                {
                    Mob = m,
                    DistToPlayer = Dist2D(px, pz, m.X, m.Z),
                    ProjAlongPath = (m.X - px) * dirX + (m.Z - pz) * dirZ
                })
                .Where(item =>
                    item.DistToPlayer <= _config.MobAvoidRadius &&
                    item.ProjAlongPath > -1.0f &&
                    item.ProjAlongPath < goalDist + 2.0f)
                .OrderBy(item => item.DistToPlayer)
                .ToList();

            if (blockingMobs.Count == 0)
                return;

            // Tenta encontrar passagem entre dois ou mais mobs se houver espaço livre entre eles
            if (blockingMobs.Count >= 2)
            {
                for (int i = 0; i < blockingMobs.Count - 1; i++)
                {
                    var a = blockingMobs[i].Mob;
                    var b = blockingMobs[i + 1].Mob;
                    float gap = Dist2D(a.X, a.Z, b.X, b.Z);

                    if (gap >= _config.MinGapBetweenMobsToPass)
                    {
                        float midX = (a.X + b.X) * 0.5f;
                        float midZ = (a.Z + b.Z) * 0.5f;

                        // Verifica se o ponto médio não está colado em um terceiro mob
                        bool thirdBlocksGap = blockingMobs.Any(other =>
                            other.Mob.ServerId != a.ServerId &&
                            other.Mob.ServerId != b.ServerId &&
                            Dist2D(midX, midZ, other.Mob.X, other.Mob.Z) < _config.MinGapBetweenMobsToPass * 0.5f);

                        if (!thirdBlocksGap)
                        {
                            outX = midX;
                            outZ = midZ;
                            avoidReason = $"Passando entre {a.Name} e {b.Name} (vão {gap:F1}m)";
                            return;
                        }
                    }
                }
            }

            // Sem vão seguro entre mobs: calcula contorno lateral (esquerda vs direita)
            var primary = blockingMobs[0].Mob;
            float perpLeftX = -dirZ;
            float perpLeftZ = dirX;
            float perpRightX = dirZ;
            float perpRightZ = -dirX;

            float step = Math.Max(4.5f, _config.MobAvoidRadius * 0.85f);
            float candLeftX = primary.X + perpLeftX * step;
            float candLeftZ = primary.Z + perpLeftZ * step;
            float candRightX = primary.X + perpRightX * step;
            float candRightZ = primary.Z + perpRightZ * step;

            float scoreLeft = ScoreSideCandidate(candLeftX, candLeftZ, goalX, goalZ, blockingMobs.Select(x => x.Mob));
            float scoreRight = ScoreSideCandidate(candRightX, candRightZ, goalX, goalZ, blockingMobs.Select(x => x.Mob));

            if (scoreLeft <= scoreRight)
            {
                outX = candLeftX;
                outZ = candLeftZ;
                avoidReason = $"Desviando pela esquerda de {primary.Name}";
            }
            else
            {
                outX = candRightX;
                outZ = candRightZ;
                avoidReason = $"Desviando pela direita de {primary.Name}";
            }
        }

        private static float ScoreSideCandidate(float cx, float cz, float goalX, float goalZ, IEnumerable<WorldEntityInfo> mobs)
        {
            float distToGoal = Dist2D(cx, cz, goalX, goalZ);
            float nearestMobPenalty = 0f;
            foreach (var m in mobs)
            {
                float d = Dist2D(cx, cz, m.X, m.Z);
                if (d < 4.0f)
                    nearestMobPenalty += (4.0f - d) * 15f;
            }
            return distToGoal + nearestMobPenalty;
        }

        public static float Dist2D(float x1, float z1, float x2, float z2)
        {
            float dx = x2 - x1;
            float dz = z2 - z1;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}