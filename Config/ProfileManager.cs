using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Full_Bot
{
    public enum LootFilterMode
    {
        Nenhum = 0,
        SomenteMoedas = 1,
        ListaEspecifica = 2,
        TodosNoRaio = 3
    }

    public class CharacterProfile
    {
        public bool AutoAcceptParty { get; set; } = true;
        public List<uint> AllowedPartyLeaderIds { get; set; } = new List<uint>();

        public bool AutoInviteFriends { get; set; } = false;
        public List<uint> AutoInviteFriendIds { get; set; } = new List<uint>();

        public bool HelpAlliesInQuest { get; set; } = true;
        public string Role { get; set; } = "Solo"; // "Solo", "Lider", "Seguidor"
        public uint LeaderPlayerId { get; set; } = 0;
        public float MaxLeaderDistance { get; set; } = 100f;

        // Metas de combate, defesa e área/voo
        public bool AutoMobPull { get; set; } = false;
        public float MobPullRadius { get; set; } = 22f;

        public bool AutoDefend { get; set; } = true;
        public float AggroDetectRadius { get; set; } = 16f;

        public bool HasAreaCenter { get; set; } = false;
        public float AreaCenterX { get; set; } = 0f;
        public float AreaCenterY { get; set; } = 0f;
        public float AreaCenterZ { get; set; } = 0f;
        public float AreaRadius { get; set; } = 45f;

        public bool UseFlightForLongDistance { get; set; } = true;
        public float FlyMinDistance { get; set; } = 60f;
        public int FlyAltitude { get; set; } = 25;

        // Loot, buffs, skills e mobs
        public LootFilterMode LootMode { get; set; } = LootFilterMode.SomenteMoedas;
        public float LootRadius { get; set; } = 6.0f;
        public List<uint> CollectItemIds { get; set; } = new List<uint>();

        public List<AutoBuffRule> BuffRules { get; set; } = new List<AutoBuffRule>();
        public List<uint> AttackSkills { get; set; } = new List<uint>();
        public List<uint> FarmMobIds { get; set; } = new List<uint>();
    }

    public static class ProfileManager
    {
        private static readonly string ProfilesFolder = "Profiles";
        private static readonly string DefaultFile = Path.Combine(ProfilesFolder, "default.json");

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static void EnsureDefaultExists()
        {
            if (!Directory.Exists(ProfilesFolder))
                Directory.CreateDirectory(ProfilesFolder);

            if (!File.Exists(DefaultFile))
            {
                var def = new CharacterProfile
                {
                    AutoAcceptParty = true,
                    AllowedPartyLeaderIds = new List<uint>(),
                    AutoInviteFriends = false,
                    AutoInviteFriendIds = new List<uint>(),
                    HelpAlliesInQuest = true,
                    Role = "Solo",
                    MaxLeaderDistance = 100f,
                    AutoMobPull = false,
                    MobPullRadius = 22f,
                    AutoDefend = true,
                    AggroDetectRadius = 16f,
                    HasAreaCenter = false,
                    AreaCenterX = 0f,
                    AreaCenterY = 0f,
                    AreaCenterZ = 0f,
                    AreaRadius = 45f,
                    UseFlightForLongDistance = true,
                    FlyMinDistance = 60f,
                    FlyAltitude = 25,
                    LootMode = LootFilterMode.SomenteMoedas,
                    LootRadius = 6.0f,
                    CollectItemIds = new List<uint>(),
                    BuffRules = new List<AutoBuffRule>
                    {
                        new AutoBuffRule { IconId = 65, SkillId = 77, BuffPartyAllies = true }
                    },
                    AttackSkills = new List<uint>(),
                    FarmMobIds = new List<uint>()
                };
                File.WriteAllText(DefaultFile, JsonSerializer.Serialize(def, JsonOpts));
            }
        }

        public static string GetProfilePath(string charName)
        {
            EnsureDefaultExists();
            if (string.IsNullOrWhiteSpace(charName) || charName.StartsWith("PID:"))
                return DefaultFile;

            foreach (char c in Path.GetInvalidFileNameChars())
                charName = charName.Replace(c, '_');

            string charPath = Path.Combine(ProfilesFolder, $"{charName}.json");
            if (!File.Exists(charPath))
                File.Copy(DefaultFile, charPath);

            return charPath;
        }

        public static CharacterProfile LoadForChar(string charName)
        {
            string path = GetProfilePath(charName);
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CharacterProfile>(json, JsonOpts) ?? new CharacterProfile();
        }

        public static void SaveForChar(string charName, CharacterProfile profile)
        {
            string path = GetProfilePath(charName);
            File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOpts));
        }
    }
}