using System.IO;
using System.Text.Json;

namespace Full_Bot
{
    public class PlayerDefinition
    {
        public string Role { get; set; } = "Solo"; // "Solo", "Lider", "Seguidor"
        public uint LeaderPlayerId { get; set; } = 0;
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
                var def = new PlayerDefinition();
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

        public static PlayerDefinition LoadForChar(string charName)
        {
            string path = GetProfilePath(charName);
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PlayerDefinition>(json, JsonOpts) ?? new PlayerDefinition();
        }

        public static void SaveForChar(string charName, PlayerDefinition definition)
        {
            string path = GetProfilePath(charName);
            File.WriteAllText(path, JsonSerializer.Serialize(definition ?? new PlayerDefinition(), JsonOpts));
        }
    }
}