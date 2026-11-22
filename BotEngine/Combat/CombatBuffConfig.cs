using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Full_Bot
{
    public enum BuffTargetType
    {
        SelfOrAoENoTarget = 0,
        SingleTargetIncludeSelf = 1
    }

    public class BuffEntryConfig
    {
        public string Name { get; set; } = "Buff";
        public uint SkillId { get; set; }
        public ushort IconId { get; set; }
        public BuffTargetType TargetType { get; set; } = BuffTargetType.SelfOrAoENoTarget;
        public bool IncludeSelf { get; set; } = true;
        public bool Enabled { get; set; } = true;
    }

    public class CombatBuffConfig
    {
        public List<BuffEntryConfig> Buffs { get; set; } = new List<BuffEntryConfig>();

        // Estilo de combate e conjuração do personagem
        public CombatArchetype Archetype { get; set; } = CombatArchetype.Fisico;
        public CombatCasterKind CasterKind { get; set; } = CombatCasterKind.PhysicalOrArcher;

        // Solo
        public bool SoloBuffEnabled { get; set; } = true;
        public bool OnlyBuffWhenNotKillingMob { get; set; } = true;
        public float SoloSafeDistanceFromMobs { get; set; } = 18.0f;

        // Grupo
        public bool GroupBuffEnabled { get; set; } = true;
        public bool GroupGatherNearLeader { get; set; } = true;
        public float GroupGatherMinDistanceToLeader { get; set; } = 6.0f;
        public double GroupSyncWaitSeconds { get; set; } = 8.0;

        // Anti-spam de cast
        public double BuffCastCooldownSeconds { get; set; } = 0.85;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string GetConfigFilePath(string charName)
        {
            string folder = Path.Combine("Config", "Buffs");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            if (string.IsNullOrWhiteSpace(charName) || charName.StartsWith("PID:"))
                return Path.Combine(folder, "default.json");

            string safeName = charName;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(invalid, '_');

            return Path.Combine(folder, $"{safeName}.json");
        }

        public static CombatBuffConfig LoadForChar(string charName)
        {
            string path = GetConfigFilePath(charName);
            if (!File.Exists(path))
            {
                var def = new CombatBuffConfig();
                def.SaveForChar(charName);
                return def;
            }

            try
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<CombatBuffConfig>(json, JsonOptions) ?? new CombatBuffConfig();
                if (loaded.Buffs == null)
                    loaded.Buffs = new List<BuffEntryConfig>();
                return loaded;
            }
            catch
            {
                return new CombatBuffConfig();
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