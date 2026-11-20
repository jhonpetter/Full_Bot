using System.Collections.Generic;

namespace Full_Bot
{
    public partial class BotPartyFollow
    {
        public bool AutoAcceptParty { get; set; }
        public List<uint> AllowedPartyLeaderIds { get; } = new List<uint>();

        public bool AutoInviteFriends { get; set; }
        public List<uint> AutoInviteFriendIds { get; } = new List<uint>();

        public bool IsLeader { get; set; }
        public uint LeaderPlayerId { get; set; }
        public float MaxLeaderDistance { get; set; } = 15f;

        public List<AutoBuffRule> BuffRules { get; } = new List<AutoBuffRule>();
    }
}