using System.Collections.Generic;

namespace Full_Bot
{
    public partial class BotCombat
    {
        public bool HelpAlliesInQuest { get; set; }
        public List<uint> AttackSkillRotation { get; } = new List<uint>();
        public List<uint> FarmMobTemplateIds { get; } = new List<uint>();
    }
}