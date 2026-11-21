using System;
using System.Collections.Generic;

namespace Full_Bot
{
    public class RouteWaypoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public DateTime RecordedAtUtc { get; set; }
    }

    public class MoveRouteRecorder
    {
        private readonly PlayerCore _player;
        private readonly MoveConfig _config;

        private readonly List<RouteWaypoint> _waypoints = new List<RouteWaypoint>();
        private DateTime _lastSampleUtc = DateTime.MinValue;

        public IReadOnlyList<RouteWaypoint> Waypoints => _waypoints;

        public MoveRouteRecorder(PlayerCore player, MoveConfig config)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void ClearRoute()
        {
            _waypoints.Clear();
            _lastSampleUtc = DateTime.MinValue;
        }

        public bool TryRecordTick(out string status)
        {
            status = string.Empty;

            if (!_config.RecordMattRouteEnabled)
                return false;

            // Gravação focada em rota no solo
            if (_player.MoveMode != 0)
                return false;

            DateTime now = DateTime.UtcNow;
            double interval = Math.Max(1.0, _config.RouteSampleIntervalSeconds);
            if ((now - _lastSampleUtc).TotalSeconds < interval)
                return false;

            _lastSampleUtc = now;

            float px = _player.X;
            float py = _player.Y;
            float pz = _player.Z;

            // Evita duplicidade caso já exista ponto dentro do raio configurado
            float dupRadius = Math.Max(0.5f, _config.RouteDuplicateRadius);
            for (int i = 0; i < _waypoints.Count; i++)
            {
                var wp = _waypoints[i];
                if (MoveObstacleAvoidance.Dist2D(px, pz, wp.X, wp.Z) <= dupRadius)
                {
                    status = $"Rota matts: ponto ignorado por duplicidade (raio {dupRadius:F1}m).";
                    return false;
                }
            }

            _waypoints.Add(new RouteWaypoint
            {
                X = px,
                Y = py,
                Z = pz,
                RecordedAtUtc = now
            });

            status = $"Rota matts: ponto #{_waypoints.Count} gravado ({px:F1}, {pz:F1}).";
            return true;
        }
    }
}