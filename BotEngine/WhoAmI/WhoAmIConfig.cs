using System;
using System.IO;
using System.Text.Json;

namespace Full_Bot
{
    public enum WhoAmIRole
    {
        Tank = 0,
        Assistant = 1,
        Damage = 2,
        Support = 3
    }

    public enum EncounterContext
    {
        SoloOpenWorld = 0,
        GroupOpenWorld = 1,
        DungeonOrBoss = 2
    }

    public class SpecializedSkillsConfig
    {
        // Tank (1.3): manter foco no mob/boss + ganchos futuros de cancelamento de AoE e conjuração
        public bool EnableTankTaunt { get; set; } = true;
        public uint TankTauntSkillId { get; set; } = 0;
        public int TankTauntCooldownMs { get; set; } = 3000;

        // Expansão futura (falta offsets no client)
        public bool EnableBossInterruptCast { get; set; } = false;
        public uint BossInterruptSkillId { get; set; } = 0;
        public bool EnableBossCancelAoE { get; set; } = false;
        public uint BossCancelAoESkillId { get; set; } = 0;

        // Support (1.3): cura focada no Tank, cura geral e reviver
        public bool EnableSingleHeal { get; set; } = true;
        public uint SingleHealSkillId { get; set; } = 0;
        public float HealTankBelowHpPercent { get; set; } = 75f;
        public float HealMemberBelowHpPercent { get; set; } = 65f;

        public bool EnableGroupHeal { get; set; } = true;
        public uint GroupHealSkillId { get; set; } = 0;
        public float GroupHealBelowHpPercent { get; set; } = 70f;
        public int GroupHealMinInjuredMembers { get; set; } = 2;

        public bool EnableRevive { get; set; } = true;
        public uint ReviveSkillId { get; set; } = 0;
        public int ReviveCooldownMs { get; set; } = 4000;

        // Assistant & Damage (1.1 / 1.3)
        public bool AssistantWaitForTankEngage { get; set; } = true;
        public float AssistantFollowDistance { get; set; } = 4.0f;
        public bool DamageWaitForAssistantOrTank { get; set; } = true;
        public bool DamageRescueSupportWhenThreatened { get; set; } = true;
        public float SupportThreatDistance { get; set; } = 10.0f;
        public float SupportFleeHpPercent { get; set; } = 60f;
    }

    public class DistanceAndKitingConfig
    {
        // 1.4 Manter distância de mobs (Support e classes frágeis)
        public bool KeepDistanceFromMobs { get; set; } = false;
        public float PreferredCombatDistance { get; set; } = 12.0f;
        public float MinSafeMobDistance { get; set; } = 8.0f;
        public float KiteStepDistance { get; set; } = 6.0f;
        public float SupportRearOffsetFromTank { get; set; } = 10.0f;
    }

    public class WhoAmIConfig
    {
        // Papel explícito do char quando estiver em grupo (PT)
        public bool GroupRoleEnabled { get; set; } = true;
        public WhoAmIRole GroupRole { get; set; } = WhoAmIRole.Damage;

        // Mantido alinhado ao GroupRole para retrocompatibilidade
        public WhoAmIRole Role
        {
            get => GroupRole;
            set => GroupRole = value;
        }

        public float BossHpThresholdToTreatAsBoss { get; set; } = 500000f;
        public bool AutoDetectBossByNameOrHp { get; set; } = true;

        public DistanceAndKitingConfig Distance { get; set; } = new DistanceAndKitingConfig();
        public SpecializedSkillsConfig Skills { get; set; } = new SpecializedSkillsConfig();

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string GetConfigFilePath(string charName)
        {
            string folder = Path.Combine("Config", "WhoAmI");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            if (string.IsNullOrWhiteSpace(charName) || charName.StartsWith("PID:"))
                return Path.Combine(folder, "default.json");

            string safeName = charName;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(invalid, '_');

            return Path.Combine(folder, $"{safeName}.json");
        }

        public static WhoAmIConfig LoadForChar(string charName)
        {
            string path = GetConfigFilePath(charName);
            if (!File.Exists(path))
            {
                var def = new WhoAmIConfig();
                def.SaveForChar(charName);
                return def;
            }

            try
            {
                string json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<WhoAmIConfig>(json, JsonOptions) ?? new WhoAmIConfig();
                if (cfg.Distance == null) cfg.Distance = new DistanceAndKitingConfig();
                if (cfg.Skills == null) cfg.Skills = new SpecializedSkillsConfig();
                return cfg;
            }
            catch
            {
                return new WhoAmIConfig();
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