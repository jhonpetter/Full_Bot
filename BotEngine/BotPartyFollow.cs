using System;
using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public partial class BotPartyFollow
    {
        private const float FollowDistance = 5.0f;

        private readonly PlayerCore _player;
        private readonly PlayerParty _party;
        private readonly WorldEntities _world;

        public uint LeaderId { get; set; }
        public List<uint> AllowedLeaderIds { get; } = new List<uint>();

        public BotPartyFollow(PlayerCore player, PlayerParty party, WorldEntities world)
        {
            _player = player;
            _party = party;
            _world = world;
        }

        public void Tick()
        {
            _party.TryAcceptPendingInvite(AllowedLeaderIds);

            if (LeaderId == 0) return;

            var member = _party.GetMembers().FirstOrDefault(m => m.Id == LeaderId);
            if (member == null || member.HP == 0) return;

            float dx = member.X - _player.X;
            float dy = member.Y - _player.Y;
            float dz = member.Z - _player.Z;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (dist > FollowDistance)
                _player.MoveTo(member.X, member.Y, member.Z);
        }
    }
}