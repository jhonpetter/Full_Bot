using System;
using System.Collections.Generic;

namespace Full_Bot
{
    public class MoveFarmArea
    {
        private readonly PlayerCore _player;
        private readonly MoveConfig _config;
        private readonly MoveNavigation _navigation;

        public MoveFarmArea(PlayerCore player, MoveConfig config, MoveNavigation navigation)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        public float DistanceFromCenter2D(float x, float z)
        {
            return MoveObstacleAvoidance.Dist2D(x, z, _config.FarmCenterX, _config.FarmCenterZ);
        }

        public bool IsTargetInsideArea(float targetX, float targetZ)
        {
            if (!_config.HasFarmArea)
                return true;

            return DistanceFromCenter2D(targetX, targetZ) <= _config.FarmRadius;
        }

        public bool CanCollectItem(uint itemId, bool isCoin, bool isMatt)
        {
            switch (_config.AreaLootMode)
            {
                case LootAreaMode.SomenteMoedas:
                    return isCoin;

                case LootAreaMode.Tudo:
                    return true;

                case LootAreaMode.MoedasEMattsLista:
                    if (isCoin) return true;
                    if (isMatt && !_config.IncludeMattsInAreaLoot) return false;
                    return _config.AllowedLootIds != null && _config.AllowedLootIds.Contains(itemId);

                default:
                    return false;
            }
        }

        public bool EnforcePerimeter(HashSet<uint> farmMobTemplateIds, out BotState state, out string status)
        {
            state = BotState.Moving;
            status = string.Empty;

            if (!_config.HasFarmArea)
                return false;

            float distFromCenter = DistanceFromCenter2D(_player.X, _player.Z);
            if (distFromCenter <= _config.FarmRadius)
                return false;

            if (_navigation != null)
            {
                return _navigation.NavigateTo(
                    _config.FarmCenterX,
                    _player.Y,
                    _config.FarmCenterZ,
                    ignoreServerId: 0u,
                    ignoreTemplateIds: farmMobTemplateIds,
                    out state,
                    out status);
            }

            _player.MoveTo(_config.FarmCenterX, _player.Y, _config.FarmCenterZ);
            status = $"Retornando para área de farm ({distFromCenter:F0}m / raio {_config.FarmRadius:F0}m)...";
            return true;
        }
    }
}