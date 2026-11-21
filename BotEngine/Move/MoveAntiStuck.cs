using System;

namespace Full_Bot
{
    public class MoveAntiStuck
    {
        private const uint MoveModeFlying = 2u;

        private readonly PlayerCore _player;
        private readonly MoveConfig _config;
        private readonly Action _toggleFlightAction;

        private DateTime _windowStartUtc = DateTime.UtcNow;
        private float _anchorX;
        private float _anchorZ;
        private bool _hasAnchor = false;

        private int _stuckAttempts = 0;
        private float _orbitAngleRad = 0f;
        private DateTime _activeEscapeUntilUtc = DateTime.MinValue;
        private float _escapeTargetX;
        private float _escapeTargetY;
        private float _escapeTargetZ;
        private bool _escapeUsedFlight = false;

        public MoveAntiStuck(PlayerCore player, MoveConfig config, Action toggleFlightAction)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _toggleFlightAction = toggleFlightAction;
        }

        public void Reset()
        {
            _hasAnchor = false;
            _stuckAttempts = 0;
            _activeEscapeUntilUtc = DateTime.MinValue;
            _escapeUsedFlight = false;
            _windowStartUtc = DateTime.UtcNow;
        }

        public bool UpdateAndRecover(bool isTryingToMove, out BotState state, out string status)
        {
            state = BotState.Moving;
            status = string.Empty;

            if (!_config.AntiStuckEnabled)
                return false;

            DateTime now = DateTime.UtcNow;
            float px = _player.X;
            float py = _player.Y;
            float pz = _player.Z;

            // Se já está executando uma manobra de desprendimento ativa, mantém o comando até vencer o tempo curto
            if (now < _activeEscapeUntilUtc)
            {
                if (_escapeUsedFlight && _player.MoveMode == MoveModeFlying)
                {
                    _player.MoveToFly(_escapeTargetX, _escapeTargetZ, _config.FlyAltitude);
                    status = $"Anti-stuck: voando para desprender (tentativa #{_stuckAttempts})...";
                }
                else
                {
                    _player.MoveTo(_escapeTargetX, _escapeTargetY, _escapeTargetZ);
                    status = $"Anti-stuck: movendo em círculo para soltar (tentativa #{_stuckAttempts})...";
                }
                return true;
            }

            if (!isTryingToMove)
            {
                _hasAnchor = false;
                _stuckAttempts = 0;
                _windowStartUtc = now;
                return false;
            }

            if (!_hasAnchor)
            {
                _anchorX = px;
                _anchorZ = pz;
                _windowStartUtc = now;
                _hasAnchor = true;
                return false;
            }

            double elapsed = (now - _windowStartUtc).TotalSeconds;
            double checkWindow = Math.Max(1.5, _config.StuckCheckSeconds);
            if (elapsed < checkWindow)
                return false;

            float movedDist = MoveObstacleAvoidance.Dist2D(_anchorX, _anchorZ, px, pz);

            // Reinicia janela de medição
            _anchorX = px;
            _anchorZ = pz;
            _windowStartUtc = now;

            if (movedDist >= Math.Max(0.2f, _config.StuckMinMoveDistance))
            {
                _stuckAttempts = 0;
                return false;
            }

            // Personagem ficou parado/preso durante a janela monitorada
            _stuckAttempts++;
            _orbitAngleRad += (float)(Math.PI * 0.65); // ~117° por tentativa para varrer círculo

            float radius = Math.Max(2.5f, _config.UnstuckOrbitRadius);
            _escapeTargetX = px + (float)Math.Cos(_orbitAngleRad) * radius;
            _escapeTargetY = py;
            _escapeTargetZ = pz + (float)Math.Sin(_orbitAngleRad) * radius;
            _activeEscapeUntilUtc = now.AddSeconds(1.8);

            // A partir da 2ª tentativa seguida preso, tenta usar voo se permitido
            if (_stuckAttempts >= 2 && _config.UnstuckAllowFlight && _player.MoveMode != MoveModeFlying)
            {
                _toggleFlightAction?.Invoke();
                _escapeUsedFlight = true;
                status = $"Anti-stuck: ativando voo para tentar soltar (tentativa #{_stuckAttempts})...";
                return true;
            }

            if (_escapeUsedFlight && _player.MoveMode == MoveModeFlying)
            {
                _player.MoveToFly(_escapeTargetX, _escapeTargetZ, _config.FlyAltitude);
                status = $"Anti-stuck: deslocando em voo para soltar (tentativa #{_stuckAttempts})...";
                return true;
            }

            _escapeUsedFlight = false;
            _player.MoveTo(_escapeTargetX, _escapeTargetY, _escapeTargetZ);
            status = $"Anti-stuck: preso ({movedDist:F1}m em {elapsed:F1}s) -> círculo (tentativa #{_stuckAttempts})...";
            return true;
        }
    }
}