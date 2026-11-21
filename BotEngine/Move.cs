using System;
using System.Collections.Generic;
using System.Reflection;

namespace Full_Bot
{
    public class Move
    {
        private readonly PlayerCore _player;
        private readonly WorldEntities _world;

        public MoveConfig Config { get; }
        public MoveObstacleAvoidance Avoidance { get; }
        public MoveNavigation Navigation { get; }
        public MoveFarmArea FarmArea { get; }
        public MoveRouteRecorder RouteRecorder { get; }
        public MoveAntiStuck AntiStuck { get; }

        public Move(PlayerCore player, WorldEntities world, MoveConfig config = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _world = world;
            Config = config ?? new MoveConfig();

            Avoidance = new MoveObstacleAvoidance(_player, _world, Config);
            Navigation = new MoveNavigation(_player, Config, Avoidance, ToggleFlightSafe);
            FarmArea = new MoveFarmArea(_player, Config, Navigation);
            RouteRecorder = new MoveRouteRecorder(_player, Config);
            AntiStuck = new MoveAntiStuck(_player, Config, ToggleFlightSafe);
        }

        public bool IsTargetAllowedInArea(float targetX, float targetZ)
        {
            return FarmArea.IsTargetInsideArea(targetX, targetZ);
        }

        public bool CanCollectInArea(uint itemId, bool isCoin, bool isMatt)
        {
            return FarmArea.CanCollectItem(itemId, isCoin, isMatt);
        }

        public bool GoTo(
            float x,
            float y,
            float z,
            out BotState state,
            out string status,
            uint ignoreServerId = 0u,
            HashSet<uint> ignoreTemplateIds = null)
        {
            if (AntiStuck.UpdateAndRecover(isTryingToMove: true, out state, out status))
                return true;

            return Navigation.NavigateTo(x, y, z, ignoreServerId, ignoreTemplateIds, out state, out status);
        }

        public bool PulseAreaAndMovement(
            HashSet<uint> farmMobTemplateIds,
            bool isEngagedInCombatOrPull,
            out BotState state,
            out string status)
        {
            state = BotState.Moving;
            status = string.Empty;

            // Grava rota de matts no solo a cada ~5s (se habilitado), sem bloquear o fluxo principal
            RouteRecorder.TryRecordTick(out _);

            // Se o personagem já saiu do perímetro da área de farm/mobada, verifica anti-stuck e retorna
            bool outsideArea = Config.HasFarmArea &&
                               FarmArea.DistanceFromCenter2D(_player.X, _player.Z) > Config.FarmRadius;

            if (outsideArea)
            {
                if (AntiStuck.UpdateAndRecover(isTryingToMove: true, out state, out status))
                    return true;

                return FarmArea.EnforcePerimeter(farmMobTemplateIds, out state, out status);
            }

            // Mesmo dentro da área, se o jogador estiver tentando andar/puxar mob e ficar preso, tenta soltar
            if (!isEngagedInCombatOrPull && AntiStuck.UpdateAndRecover(isTryingToMove: false, out state, out status))
                return true;

            return false;
        }

        private void ToggleFlightSafe()
        {
            var asmField = typeof(PlayerCore).GetField("_asm", BindingFlags.NonPublic | BindingFlags.Instance);
            var memField = typeof(PlayerCore).GetField("_mem", BindingFlags.NonPublic | BindingFlags.Instance);
            var asm = asmField?.GetValue(_player) as AsmRunner;
            var mem = memField?.GetValue(_player) as MemoryProcess;
            if (asm != null && mem != null)
            {
                asm.RunScript(mem, @"Scripts\ToggleFly.asm");
            }
        }
    }
}