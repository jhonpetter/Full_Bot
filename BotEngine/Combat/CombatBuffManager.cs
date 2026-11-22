using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Full_Bot
{
    public class CombatBuffManager
    {
        private static readonly ConcurrentDictionary<uint, bool> _groupMemberBuffReady = new ConcurrentDictionary<uint, bool>();
        private static readonly ConcurrentDictionary<uint, DateTime> _groupMemberBuffStamp = new ConcurrentDictionary<uint, DateTime>();

        private readonly PlayerCore _player;
        private readonly PlayerBuffs _buffs;
        private readonly PlayerParty _party;
        private readonly WorldEntities _world;
        private readonly CombatBuffConfig _config;

        private DateTime _lastCastUtc = DateTime.MinValue;
        private int _partyTargetCursor = 0;
        private DateTime _groupBuffStartUtc = DateTime.MinValue;

        public CombatBuffManager(
            PlayerCore player,
            PlayerBuffs buffs,
            PlayerParty party,
            WorldEntities world,
            CombatBuffConfig config)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _buffs = buffs;
            _party = party;
            _world = world;
            _config = config ?? new CombatBuffConfig();
        }

        public bool HasFinishedGroupBuffs
        {
            get
            {
                uint pid = _player.Id;
                if (pid == 0) return true;
                return _groupMemberBuffReady.TryGetValue(pid, out bool ready) && ready;
            }
        }

        public void SetFinishedGroupBuffs(bool finished)
        {
            uint pid = _player.Id;
            if (pid == 0) return;
            _groupMemberBuffReady[pid] = finished;
            _groupMemberBuffStamp[pid] = DateTime.UtcNow;
        }

        public bool IsInParty()
        {
            if (_party == null) return false;
            try
            {
                var members = _party.GetMembers();
                return members != null && members.Count > 1;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidCoord(float x, float z)
        {
            return !float.IsNaN(x) && !float.IsInfinity(x) &&
                   !float.IsNaN(z) && !float.IsInfinity(z) &&
                   (Math.Abs(x) > 0.01f || Math.Abs(z) > 0.01f);
        }

        public bool TryProcessBuffs(bool isCurrentlyKillingMob, out BotState desiredState, out string status)
        {
            desiredState = BotState.Idle;
            status = string.Empty;

            if (_player.Idle != 0)
                return false;

            bool inParty = IsInParty();

            // Modo Solo: só buffa se permitido e fora de combate quando configurado
            if (!inParty)
            {
                _groupBuffStartUtc = DateTime.MinValue;
                SetFinishedGroupBuffs(true);

                if (!_config.SoloBuffEnabled)
                    return false;

                if (_config.OnlyBuffWhenNotKillingMob && (isCurrentlyKillingMob || _player.TargetId != 0))
                    return false;

                if (HasNearbyMobCloserThan(_config.SoloSafeDistanceFromMobs, out float nearestMobDist, out float mx, out _, out float mz))
                {
                    float fleeThreshold = Math.Min(6.0f, Math.Max(1.0f, _config.SoloSafeDistanceFromMobs));
                    if (nearestMobDist < fleeThreshold && IsValidCoord(mx, mz) && IsValidCoord(_player.X, _player.Z))
                    {
                        float dx = _player.X - mx;
                        float dz = _player.Z - mz;
                        float len = (float)Math.Sqrt(dx * dx + dz * dz);
                        if (len > 0.01f)
                        {
                            float step = Math.Max(6.0f, _config.SoloSafeDistanceFromMobs * 0.5f);
                            _player.MoveTo(_player.X + (dx / len) * step, _player.Y, _player.Z + (dz / len) * step);
                            desiredState = BotState.Moving;
                            status = $"Afastando de mob ({nearestMobDist:F1}m) para aplicar buff solo...";
                            return true;
                        }
                    }
                }

                return TryApplySoloBuffs(out status);
            }

            // Modo Grupo: reunir próximo ao líder (índice 0), aplicar buffs e sincronizar término
            if (!_config.GroupBuffEnabled)
            {
                _groupBuffStartUtc = DateTime.MinValue;
                SetFinishedGroupBuffs(true);
                return false;
            }

            if (_groupBuffStartUtc == DateTime.MinValue)
                _groupBuffStartUtc = DateTime.UtcNow;

            if (_config.GroupGatherNearLeader && TryGatherNearLeader(out desiredState, out status))
            {
                SetFinishedGroupBuffs(false);
                return true;
            }

            if (TryApplyGroupBuffs(out status))
            {
                SetFinishedGroupBuffs(false);
                return true;
            }

            SetFinishedGroupBuffs(true);
            if (!AreGroupMembersReady(out int pendingCount))
            {
                double elapsed = (DateTime.UtcNow - _groupBuffStartUtc).TotalSeconds;
                if (elapsed < Math.Max(1.0, _config.GroupSyncWaitSeconds))
                {
                    desiredState = BotState.WaitingGroup;
                    status = $"Buffs concluídos; aguardando {pendingCount} membro(s) do grupo ({elapsed:F1}s)...";
                    return true;
                }
            }

            _groupBuffStartUtc = DateTime.MinValue;
            return false;
        }

        private bool TryApplySoloBuffs(out string status)
        {
            status = string.Empty;
            if ((DateTime.UtcNow - _lastCastUtc).TotalSeconds < Math.Max(0.2, _config.BuffCastCooldownSeconds))
                return false;

            if (_config.Buffs == null || _config.Buffs.Count == 0)
                return false;

            var activeIcons = new HashSet<ushort>(_buffs?.GetActiveBuffs()?.Select(b => b.IconId) ?? Enumerable.Empty<ushort>());

            foreach (var rule in _config.Buffs)
            {
                if (rule == null || !rule.Enabled || rule.SkillId == 0)
                    continue;

                if (rule.IconId != 0 && activeIcons.Contains(rule.IconId))
                    continue;

                _lastCastUtc = DateTime.UtcNow;
                _player.CastSkill(rule.SkillId);
                status = $"[Solo Buff] Aplicando {rule.Name} (Skill {rule.SkillId} / Icon {rule.IconId})";
                return true;
            }

            return false;
        }

        private bool TryGatherNearLeader(out BotState desiredState, out string status)
        {
            desiredState = BotState.Following;
            status = string.Empty;

            if (_party == null)
                return false;

            var members = _party.GetMembers();
            if (members == null || members.Count == 0)
                return false;

            var leader = members[0];
            if (leader == null || leader.Id == 0 || leader.Id == _player.Id)
                return false;

            if (!IsValidCoord(leader.X, leader.Z) || !IsValidCoord(_player.X, _player.Z))
                return false;

            string leaderLabel = _world != null ? _world.ResolvePartyLeaderName(leader.Id) : $"ID:{leader.Id}";
            float distToLeader = MoveObstacleAvoidance.Dist2D(_player.X, _player.Z, leader.X, leader.Z);
            float minDist = Math.Max(1.5f, _config.GroupGatherMinDistanceToLeader);

            if (distToLeader > minDist)
            {
                _player.MoveTo(leader.X, leader.Y, leader.Z);
                status = $"Reunindo próximo ao líder {leaderLabel} ({distToLeader:F1}m > {minDist:F1}m) para buffs...";
                return true;
            }

            return false;
        }

        private bool TryApplyGroupBuffs(out string status)
        {
            status = string.Empty;
            if ((DateTime.UtcNow - _lastCastUtc).TotalSeconds < Math.Max(0.2, _config.BuffCastCooldownSeconds))
                return false;

            if (_config.Buffs == null || _config.Buffs.Count == 0)
                return false;

            var activeIcons = new HashSet<ushort>(_buffs?.GetActiveBuffs()?.Select(b => b.IconId) ?? Enumerable.Empty<ushort>());
            var partyMembers = _party?.GetMembers() ?? new List<PartyMemberInfo>();
            var memberById = new Dictionary<uint, PartyMemberInfo>();

            foreach (var m in partyMembers)
            {
                if (m != null && m.Id != 0 && !memberById.ContainsKey(m.Id))
                    memberById[m.Id] = m;
            }

            foreach (var rule in _config.Buffs)
            {
                if (rule == null || !rule.Enabled || rule.SkillId == 0)
                    continue;

                if (rule.TargetType == BuffTargetType.SelfOrAoENoTarget)
                {
                    if (rule.IconId != 0 && activeIcons.Contains(rule.IconId))
                        continue;

                    _lastCastUtc = DateTime.UtcNow;
                    _player.CastSkill(rule.SkillId);
                    status = $"[Grupo AoE/Self] Skill {rule.SkillId} (Icon {rule.IconId})";
                    return true;
                }

                if (rule.IncludeSelf && _player.Id != 0)
                {
                    if (rule.IconId == 0 || !activeIcons.Contains(rule.IconId))
                    {
                        if (_player.TargetId != _player.Id)
                            _player.SelectTarget(_player.Id);

                        _lastCastUtc = DateTime.UtcNow;
                        _player.CastSkill(rule.SkillId);
                        status = $"[Grupo Target -> Self] Skill {rule.SkillId} no próprio char";
                        return true;
                    }
                }

                var allyTargets = new List<uint>();
                foreach (var member in partyMembers)
                {
                    if (member != null && member.Id != 0 && member.Id != _player.Id && member.HP > 0 && !allyTargets.Contains(member.Id))
                        allyTargets.Add(member.Id);
                }

                if (allyTargets.Count == 0)
                    continue;

                int count = allyTargets.Count;
                for (int i = 0; i < count; i++)
                {
                    int idx = (_partyTargetCursor + i) % count;
                    uint targetId = allyTargets[idx];

                    if (rule.IconId != 0 && memberById.TryGetValue(targetId, out PartyMemberInfo memberInfo))
                    {
                        if (MemberHasIcon(memberInfo, rule.IconId))
                            continue;
                    }

                    if (_player.TargetId != targetId)
                        _player.SelectTarget(targetId);

                    _lastCastUtc = DateTime.UtcNow;
                    _player.CastSkill(rule.SkillId);
                    _partyTargetCursor = (idx + 1) % count;
                    status = $"[Grupo Target] Skill {rule.SkillId} no membro ID {targetId}";
                    return true;
                }
            }

            return false;
        }

        private static bool MemberHasIcon(PartyMemberInfo member, ushort iconId)
        {
            if (member == null || iconId == 0) return false;

            var type = member.GetType();
            var prop = type.GetProperty("BuffIconIds", BindingFlags.Public | BindingFlags.Instance)
                    ?? type.GetProperty("BuffIds", BindingFlags.Public | BindingFlags.Instance);

            if (prop != null)
            {
                var val = prop.GetValue(member, null) as IEnumerable;
                if (val != null)
                {
                    foreach (var item in val)
                    {
                        if (item == null) continue;
                        try
                        {
                            if (Convert.ToUInt32(item) == iconId)
                                return true;
                        }
                        catch
                        {
                            // Ignora item incompatível
                        }
                    }
                }
            }

            return false;
        }

        private bool AreGroupMembersReady(out int pendingCount)
        {
            pendingCount = 0;
            if (_party == null) return true;

            var members = _party.GetMembers();
            if (members == null || members.Count <= 1) return true;

            DateTime now = DateTime.UtcNow;
            double maxAgeSeconds = Math.Max(5.0, _config.GroupSyncWaitSeconds * 2.0);

            foreach (var m in members)
            {
                if (m == null || m.Id == 0 || m.Id == _player.Id || m.HP == 0) continue;

                bool fresh = _groupMemberBuffStamp.TryGetValue(m.Id, out DateTime stamp) &&
                             (now - stamp).TotalSeconds <= maxAgeSeconds;
                bool ready = fresh && _groupMemberBuffReady.TryGetValue(m.Id, out bool r) && r;

                if (!ready)
                    pendingCount++;
            }

            return pendingCount == 0;
        }

        private bool HasNearbyMobCloserThan(float threshold, out float nearestDist, out float mx, out float my, out float mz)
        {
            nearestDist = float.MaxValue;
            mx = my = mz = 0f;

            if (!IsValidCoord(_player.X, _player.Z))
                return false;

            var entities = _world?.GetMobsAndNpcs();
            if (entities == null) return false;

            foreach (var e in entities)
            {
                if (e == null || e.Type != 6 || e.ServerId == 0) continue;
                if (e.HPMax > 0 && e.HP == 0) continue;
                if (!IsValidCoord(e.X, e.Z)) continue;

                float d = MoveObstacleAvoidance.Dist2D(_player.X, _player.Z, e.X, e.Z);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    mx = e.X;
                    my = e.Y;
                    mz = e.Z;
                }
            }

            return nearestDist <= threshold;
        }
    }
}