using System;
using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public class BotRoutines
    {
        private const uint MoveModeWalking = 0u;
        private const uint MoveModeSwimming = 1u;
        private const uint MoveModeFlying = 2u;

        private readonly PlayerCore _player;
        private readonly PlayerSkills _skills;
        private readonly WorldEntities _world;
        private readonly CombatModule _combat;
        private readonly GatherModule _gather;

        public bool AutoMobPull { get; set; } = false;
        public float MobPullRadius { get; set; } = 22f;

        public bool AutoDefend { get; set; } = true;
        public float AggroDetectRadius { get; set; } = 16f;

        public bool HasAreaCenter { get; set; } = false;
        public float AreaCenterX { get; set; }
        public float AreaCenterY { get; set; }
        public float AreaCenterZ { get; set; }
        public float AreaRadius { get; set; } = 45f;

        public bool UseFlightForLongDistance { get; set; } = true;
        public float FlyMinDistance { get; set; } = 60f;
        public int FlyAltitude { get; set; } = 25;

        private bool _wasFlyingForRoutine = false;
        private uint _lastHP = 0;

        public BotRoutines(
            PlayerCore player,
            PlayerSkills skills,
            WorldEntities world,
            CombatModule combat,
            GatherModule gather)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _skills = skills ?? throw new ArgumentNullException(nameof(skills));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _gather = gather ?? throw new ArgumentNullException(nameof(gather));
        }

        private float GetDistance2DToPlayer(WorldEntityInfo entity)
        {
            float coordDist = SupportModule.Distance2D(_player.X, _player.Z, entity.X, entity.Z);
            if (coordDist > 0.01f)
                return coordDist;

            return entity.Distance > 0f ? entity.Distance : 0f;
        }

        public void SetAreaCenterHere()
        {
            AreaCenterX = _player.X;
            AreaCenterY = _player.Y;
            AreaCenterZ = _player.Z;
            HasAreaCenter = true;
        }

        public bool RunAreaAndFlightRoutine(out BotState state, out string status)
        {
            state = BotState.Moving;
            status = string.Empty;

            if (!HasAreaCenter)
                return false;

            float distToCenter = SupportModule.Distance2D(_player.X, _player.Z, AreaCenterX, AreaCenterZ);
            float arrivalRadius = Math.Max(4.0f, Math.Min(AreaRadius * 0.35f, 10.0f));

            if (distToCenter <= arrivalRadius)
            {
                if (IsPlayerFlying())
                {
                    ToggleFlightSafe();
                    _wasFlyingForRoutine = false;
                    status = "Chegou ao destino e saiu do voo (MoveMode: 2 -> toggle).";
                    return true;
                }

                _wasFlyingForRoutine = false;
                return false;
            }

            if (distToCenter <= AreaRadius && !_wasFlyingForRoutine)
                return false;

            if (UseFlightForLongDistance && distToCenter >= FlyMinDistance)
            {
                if (!IsPlayerFlying())
                {
                    ToggleFlightSafe();
                    _wasFlyingForRoutine = true;
                    status = $"Ativando voo para retornar à área ({distToCenter:F0}m)...";
                    return true;
                }

                _wasFlyingForRoutine = true;
                _player.MoveToFly(AreaCenterX, AreaCenterZ, FlyAltitude);
                status = $"Voando para área ({distToCenter:F0}m | Alt: {FlyAltitude})...";
                return true;
            }

            _player.MoveTo(AreaCenterX, AreaCenterY, AreaCenterZ);
            status = $"Retornando ao perímetro da área ({distToCenter:F0}m)...";
            return true;
        }

        public bool RunCombatAndAggroRoutine(out string status)
        {
            status = string.Empty;

            uint currentHp = _player.HP;
            bool hpDropped = _lastHP > 0 && currentHp < _lastHP;
            _lastHP = currentHp;

            var allEntities = _world.GetMobsAndNpcs();
            var allowedIds = new HashSet<uint>(_combat.FarmMobTemplateIds);

            // 1. Defesa contra agressores (Type == 6) se AutoDefend estiver ativo
            if (AutoDefend)
            {
                WorldEntityInfo aggressor = null;

                if (_player.TargetId != 0)
                {
                    aggressor = allEntities.FirstOrDefault(m =>
                        m.Type == 6 &&
                        m.ServerId == _player.TargetId &&
                        GetDistance2DToPlayer(m) <= AggroDetectRadius);
                }

                if (aggressor == null && hpDropped)
                {
                    aggressor = allEntities
                        .Where(m => m.Type == 6 && m.ServerId != 0 && GetDistance2DToPlayer(m) <= Math.Min(AggroDetectRadius, 12f))
                        .OrderBy(m => GetDistance2DToPlayer(m))
                        .FirstOrDefault();
                }

                if (aggressor != null)
                {
                    _player.SelectTarget(aggressor.ServerId);
                    _combat.ExecuteAttackRotation();
                    status = $"Defendendo de {aggressor.Name} ({aggressor.HP}/{aggressor.HPMax})";
                    return true;
                }
            }

            // 2. Auto-mobada (Type == 6)
            if (AutoMobPull)
            {
                var pullCandidate = allEntities
                    .Where(m =>
                        m.Type == 6 &&
                        m.ServerId != 0 &&
                        GetDistance2DToPlayer(m) <= MobPullRadius &&
                        (allowedIds.Count == 0 || allowedIds.Contains(m.TemplateId)) &&
                        (!HasAreaCenter || SupportModule.Distance2D(m.X, m.Z, AreaCenterX, AreaCenterZ) <= AreaRadius))
                    .OrderBy(m => GetDistance2DToPlayer(m))
                    .FirstOrDefault();

                if (pullCandidate != null)
                {
                    _player.SelectTarget(pullCandidate.ServerId);
                    _combat.ExecuteAttackRotation();
                    status = $"[Mobada] {pullCandidate.Name} (ID: {pullCandidate.TemplateId} | SrvID: {pullCandidate.ServerId})";
                    return true;
                }
            }

            // 3. Lista de IDs padrão via CombatModule
            if (allowedIds.Count > 0)
            {
                float cx = HasAreaCenter ? AreaCenterX : _player.X;
                float cy = HasAreaCenter ? AreaCenterY : _player.Y;
                float cz = HasAreaCenter ? AreaCenterZ : _player.Z;

                if (_combat.TryFarmMobs(_combat.FarmMobTemplateIds, cx, cy, cz, out string farmStatus))
                {
                    status = farmStatus;
                    return true;
                }
            }

            return false;
        }

        public bool RunLootRoutine(out string status)
        {
            status = string.Empty;
            return _gather != null && _gather.TryLoot(out status);
        }

        private bool IsPlayerFlying()
        {
            uint mode = _player.MoveMode;
            return mode == MoveModeFlying;
        }

        private void ToggleFlightSafe()
        {
            var asmField = typeof(PlayerCore).GetField("_asm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var memField = typeof(PlayerCore).GetField("_mem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var asm = asmField?.GetValue(_player) as AsmRunner;
            var mem = memField?.GetValue(_player) as MemoryProcess;
            if (asm != null && mem != null)
            {
                asm.RunScript(mem, @"Scripts\ToggleFly.asm");
            }
        }
    }
}