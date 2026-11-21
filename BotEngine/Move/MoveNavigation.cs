using System;
using System.Collections.Generic;

namespace Full_Bot
{
    public class MoveNavigation
    {
        private const uint MoveModeFlying = 2u;

        private readonly PlayerCore _player;
        private readonly MoveConfig _config;
        private readonly MoveObstacleAvoidance _avoidance;
        private readonly Action _toggleFlightAction;

        private bool _navigatingWithFlight = false;

        public MoveNavigation(
            PlayerCore player,
            MoveConfig config,
            MoveObstacleAvoidance avoidance,
            Action toggleFlightAction)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _avoidance = avoidance;
            _toggleFlightAction = toggleFlightAction;
        }

        public bool IsFlying => _player.MoveMode == MoveModeFlying;

        public void ResetFlightState()
        {
            _navigatingWithFlight = false;
        }

        public bool NavigateTo(
            float targetX,
            float targetY,
            float targetZ,
            uint ignoreServerId,
            HashSet<uint> ignoreTemplateIds,
            out BotState state,
            out string status)
        {
            state = BotState.Moving;
            status = string.Empty;

            float dist = MoveObstacleAvoidance.Dist2D(_player.X, _player.Z, targetX, targetZ);
            float arrival = Math.Max(2.0f, _config.ArrivalRadius);

            // Chegou ao destino: se estiver voando (MoveMode == 2), cancela o voo
            if (dist <= arrival)
            {
                if (IsFlying)
                {
                    _toggleFlightAction?.Invoke();
                    _navigatingWithFlight = false;
                    status = "Destino alcançado: cancelando voo (MoveMode == 2).";
                    return true;
                }

                _navigatingWithFlight = false;
                return false;
            }

            // Deslocamento aéreo para longas distâncias
            if (_config.UseFlightForLongDistance && (dist >= _config.FlyMinDistance || _navigatingWithFlight))
            {
                if (!IsFlying)
                {
                    _toggleFlightAction?.Invoke();
                    _navigatingWithFlight = true;
                    status = $"Ativando voo para destino ({dist:F0}m)...";
                    return true;
                }

                _navigatingWithFlight = true;
                _player.MoveToFly(targetX, targetZ, _config.FlyAltitude);
                status = $"Voando para destino ({dist:F0}m | Alt: {_config.FlyAltitude})...";
                return true;
            }

            // Deslocamento no solo com desvio de mobs / passagem entre mobs
            float moveX = targetX;
            float moveZ = targetZ;
            string reason = string.Empty;

            if (_avoidance != null)
            {
                _avoidance.ComputeAvoidedDestination(
                    targetX,
                    targetZ,
                    ignoreServerId,
                    ignoreTemplateIds,
                    out moveX,
                    out moveZ,
                    out reason);
            }

            _player.MoveTo(moveX, targetY, moveZ);
            status = string.IsNullOrWhiteSpace(reason)
                ? $"Movendo no solo para destino ({dist:F0}m)..."
                : $"{reason} ({dist:F0}m)...";
            return true;
        }
    }
}