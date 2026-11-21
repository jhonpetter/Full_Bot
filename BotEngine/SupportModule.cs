using System;
using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public class AutoBuffRule
    {
        public ushort IconId { get; set; }
        public uint SkillId { get; set; }
        public bool BuffPartyAllies { get; set; } = false;
    }

    public class SupportModule
    {
        private readonly PlayerCore _player;
        private readonly PlayerBuffs _buffs;
        private readonly PlayerSkills _skills;
        private readonly PlayerParty _party;
        private readonly PlayerFriends _friends;

        public bool AutoAcceptParty { get; set; } = true;
        public List<uint> AllowedPartyLeaderIds { get; } = new List<uint>();

        public bool AutoInviteFriends { get; set; } = false;
        public List<uint> AutoInviteFriendIds { get; } = new List<uint>();

        public bool IsLeader { get; set; } = false;
        public uint LeaderPlayerId { get; set; } = 0;

        public float MaxLeaderDistance { get; set; } = 100f;
        public float WaitGroupRadius { get; set; } = 25f;

        public List<AutoBuffRule> BuffRules { get; } = new List<AutoBuffRule>();

        private DateTime _lastInviteCheck = DateTime.MinValue;

        public SupportModule(PlayerCore player, PlayerBuffs buffs, PlayerSkills skills, PlayerParty party, PlayerFriends friends)
        {
            _player = player;
            _buffs = buffs;
            _skills = skills;
            _party = party;
            _friends = friends;
        }

        public static float Distance2D(float x1, float z1, float x2, float z2)
        {
            float dx = x1 - x2;
            float dz = z1 - z2;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public bool TryAcceptPartyInvite(out string status)
        {
            status = string.Empty;
            if (!AutoAcceptParty) return false;

            if (_party.TryAcceptPendingInvite(AllowedPartyLeaderIds))
            {
                status = "Aceitando convite de grupo...";
                return true;
            }
            return false;
        }

        public bool TryAutoInviteFriends(out string status)
        {
            status = string.Empty;
            if (!AutoInviteFriends || AutoInviteFriendIds.Count == 0) return false;

            if ((DateTime.Now - _lastInviteCheck).TotalSeconds < 5) return false;
            _lastInviteCheck = DateTime.Now;

            var friendIds = _friends.GetFriends().Select(f => f.Id).ToHashSet();
            var partyMemberIds = _party.GetMembers().Select(m => m.Id).ToHashSet();

            foreach (uint targetId in AutoInviteFriendIds)
            {
                if (targetId == 0 || targetId == _player.Id) continue;

                if (friendIds.Contains(targetId) && !partyMemberIds.Contains(targetId))
                {
                    _party.Invite(targetId);
                    status = $"Convidando amigo (ID: {targetId}) para grupo...";
                    return true;
                }
            }
            return false;
        }

        public bool TryMaintainProximityToLeader(out string status)
        {
            status = string.Empty;
            if (IsLeader || LeaderPlayerId == 0) return false;

            var partyLeader = _party.GetMembers().FirstOrDefault(m => m.Id == LeaderPlayerId);
            float leaderX = 0, leaderY = 0, leaderZ = 0;
            bool foundLeader = false;

            if (partyLeader != null && partyLeader.HP > 0)
            {
                leaderX = partyLeader.X;
                leaderY = partyLeader.Y;
                leaderZ = partyLeader.Z;
                foundLeader = true;
            }
            else
            {
                var hubLeader = BotHub.FindBotByPlayerId(LeaderPlayerId);
                if (hubLeader != null && hubLeader.Player != null && hubLeader.Player.Base != 0 && hubLeader.Player.HP > 0)
                {
                    leaderX = hubLeader.Player.X;
                    leaderY = hubLeader.Player.Y;
                    leaderZ = hubLeader.Player.Z;
                    foundLeader = true;
                }
            }

            if (!foundLeader) return false;

            float dist = Distance2D(_player.X, _player.Z, leaderX, leaderZ);

            if (dist > MaxLeaderDistance)
            {
                _player.MoveTo(leaderX, leaderY, leaderZ);
                status = $"Aproximando do líder ({dist:F0}m > {MaxLeaderDistance:F0}m)";
                return true;
            }

            return false;
        }

        public bool ShouldWaitForGroup(out string status)
        {
            status = string.Empty;
            if (!IsLeader) return false;

            var members = _party.GetMembers();
            foreach (var m in members)
            {
                if (m.Id == _player.Id || m.HP == 0) continue;

                float dist = Distance2D(_player.X, _player.Z, m.X, m.Z);
                if (dist > WaitGroupRadius)
                {
                    status = $"Esperando grupo chegar ({dist:F0}m)...";
                    return true;
                }
            }
            return false;
        }

        public bool TryProcessBuffs(out string status)
        {
            status = string.Empty;
            if (BuffRules.Count == 0 || _player.Idle != 0) return false;

            var activeIcons = _buffs.GetActiveBuffs().Select(b => b.IconId).ToHashSet();
            uint previousTarget = _player.TargetId;

            foreach (var rule in BuffRules)
            {
                if (!activeIcons.Contains(rule.IconId) && _skills.IsSkillReady(rule.SkillId))
                {
                    if (_player.TargetId != _player.Id)
                        _player.TargetId = _player.Id;

                    _player.CastSkill(rule.SkillId);

                    if (previousTarget != 0 && previousTarget != _player.Id)
                        _player.TargetId = previousTarget;

                    status = $"Buffando a si mesmo (Skill {rule.SkillId})";
                    return true;
                }

                if (rule.BuffPartyAllies && _skills.IsSkillReady(rule.SkillId))
                {
                    var snapshot = BotHub.GetSnapshot();
                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        var ally = snapshot[i];
                        if (ally?.Player == null || ally.Player.Id == _player.Id || ally.Player.Base == 0 || ally.Player.HP == 0)
                            continue;

                        if (Distance2D(_player.X, _player.Z, ally.Player.X, ally.Player.Z) > 25f)
                            continue;

                        var allyBuffs = ally.Buffs?.GetActiveBuffs();
                        bool hasIcon = allyBuffs != null && allyBuffs.Any(b => b.IconId == rule.IconId);
                        if (!hasIcon)
                        {
                            if (_player.TargetId != ally.Player.Id)
                                _player.TargetId = ally.Player.Id;

                            _player.CastSkill(rule.SkillId);

                            if (previousTarget != 0 && previousTarget != ally.Player.Id)
                                _player.TargetId = previousTarget;

                            status = $"Buffando aliado {ally.Player.Name}";
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}