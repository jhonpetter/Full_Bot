using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Full_Bot
{
    public class CultivationQuestObjective
    {
        public uint MobOrTargetId { get; set; }
        public string TargetName { get; set; } = "";
        public int RequiredCount { get; set; } = 1;
        public int CurrentCount { get; set; } = 0;
        public bool IsBoss { get; set; } = false;
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public bool IsCompleted => CurrentCount >= Math.Max(1, RequiredCount);
    }

    public class CultivationQuestDefinition
    {
        public uint QuestId { get; set; }
        public string Title { get; set; } = "";
        public int RequiredLevel { get; set; } = 1;
        public bool AvailableInJournal { get; set; } = true;
        public bool HasQuestActive { get; set; } = true;
        public bool IsMemberReadyAtLocation { get; set; } = true;

        public float QuestX { get; set; }
        public float QuestY { get; set; }
        public float QuestZ { get; set; }

        public List<CultivationQuestObjective> Objectives { get; set; } = new List<CultivationQuestObjective>();
    }

    public class SharedQuestGuidance
    {
        public uint HelperPlayerId { get; set; }
        public string HelperName { get; set; } = "";
        public uint QuestId { get; set; }
        public uint TargetMobId { get; set; }
        public string TargetMobName { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public class SelfCultivationConfig
    {
        public bool Enabled { get; set; } = true;

        // 1.2.1 e 1.2.4 Níveis e PT
        public int MaxLevelDiffForGroup { get; set; } = 3;
        public int MinGroupCultivationLevel { get; set; } = 18;
        public int FullPartySize { get; set; } = 6;

        // 1.2.2 Aguardar membros voando alto no local da quest
        public bool AutoPickQuestFromJournal { get; set; } = true;
        public bool WaitForPartyAtQuestLocation { get; set; } = true;
        public bool PreferHoverHighWhileWaiting { get; set; } = true;
        public float FlyHoverAltitude { get; set; } = 18.0f;
        public float ArrivalRadius { get; set; } = 8.0f;
        public double PartyWaitTimeoutSeconds { get; set; } = 45.0;

        // 1.2.3 e 1.2.5 Regras de Boss e Solo
        public int MaxMultiBossTypesInQuest { get; set; } = 5;
        public bool RequireFullPartyForSingleBoss { get; set; } = true;
        public bool RequireFullPartyForMultiBoss { get; set; } = true;
        public bool SoloAvoidBosses { get; set; } = true;

        // 1.3 Ajuda quando estiver com nível muito acima dos outros na PT
        public bool HighLevelHelpOthersEnabled { get; set; } = true;
        public int HighLevelCarryDiffThreshold { get; set; } = 15;
        public bool ShareLocationAndMobsWithParty { get; set; } = true;

        // Gancho de expansão futura (ex.: leitura de quest-log/NPC/pacotes do client)
        public bool FutureQuestMemoryScanEnabled { get; set; } = false;

        // Fila/catálogo de quests configuradas para o char
        public List<CultivationQuestDefinition> Quests { get; set; } = new List<CultivationQuestDefinition>();

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static SelfCultivationConfig CreateDefault()
        {
            return new SelfCultivationConfig
            {
                Enabled = true,
                MaxLevelDiffForGroup = 3,
                MinGroupCultivationLevel = 18,
                FullPartySize = 6,
                AutoPickQuestFromJournal = true,
                WaitForPartyAtQuestLocation = true,
                PreferHoverHighWhileWaiting = true,
                FlyHoverAltitude = 18.0f,
                ArrivalRadius = 8.0f,
                PartyWaitTimeoutSeconds = 45.0,
                MaxMultiBossTypesInQuest = 5,
                RequireFullPartyForSingleBoss = true,
                RequireFullPartyForMultiBoss = true,
                SoloAvoidBosses = true,
                HighLevelHelpOthersEnabled = true,
                HighLevelCarryDiffThreshold = 15,
                ShareLocationAndMobsWithParty = true,
                FutureQuestMemoryScanEnabled = false,
                Quests = new List<CultivationQuestDefinition>()
            };
        }

        public static string GetConfigFilePath(string charName)
        {
            string folder = Path.Combine("Config", "SelfCultivation");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            if (string.IsNullOrWhiteSpace(charName) || charName.StartsWith("PID:"))
                return Path.Combine(folder, "default.json");

            string safeName = charName;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(invalid, '_');

            return Path.Combine(folder, $"{safeName}.json");
        }

        public static SelfCultivationConfig LoadForChar(string charName)
        {
            string path = GetConfigFilePath(charName);
            if (!File.Exists(path))
            {
                var def = CreateDefault();
                def.SaveForChar(charName);
                return def;
            }

            try
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<SelfCultivationConfig>(json, JsonOptions);
                if (loaded == null)
                    return CreateDefault();
                if (loaded.Quests == null)
                    loaded.Quests = new List<CultivationQuestDefinition>();
                return loaded;
            }
            catch
            {
                return CreateDefault();
            }
        }

        public void SaveForChar(string charName)
        {
            string path = GetConfigFilePath(charName);
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(path, json);
        }
    }
}