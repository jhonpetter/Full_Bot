using System.Collections.Generic;

namespace Full_Bot
{
    public partial class BotQuestRunner
    {
        public LootFilterMode Mode { get; set; } = LootFilterMode.SomenteMoedas;
        public float LootRadius { get; set; } = 12f;
        public List<uint> CollectItemIds { get; } = new List<uint>();
    }
}