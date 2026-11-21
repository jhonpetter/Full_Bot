using System;
using System.Linq;
using System.Windows.Forms;

namespace Full_Bot
{
    public static class BotEngineModulesUiHelper
    {
        public static BotController ResolveTargetBot(uint playerId = 0)
        {
            if (playerId != 0)
            {
                var byId = BotHub.FindBotByPlayerId(playerId);
                if (byId != null)
                    return byId;
            }

            var snap = BotHub.GetSnapshot();
            if (snap == null || snap.Count == 0)
                return null;

            return snap.FirstOrDefault(b => b?.Player != null && b.Player.Base != 0) ?? snap[0];
        }

        private static string GetCharKey(BotController bot)
        {
            string name = bot?.Player?.Name;
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("PID:"))
                return "default";
            return name;
        }

        public static void OpenWhoAmISettings(IWin32Window owner = null, uint playerId = 0)
        {
            var bot = ResolveTargetBot(playerId);
            if (bot == null)
            {
                MessageBox.Show(owner, "Nenhum processo/personagem conectado no BotHub.", "WhoAmI", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bot.InitializeNewModules();
            string charKey = GetCharKey(bot);
            var cfg = WhoAmIConfig.LoadForChar(charKey);

            using (var frm = new WhoAmISettingsForm(charKey, cfg, () =>
            {
                cfg.SaveForChar(charKey);
                bot.Modules?.LoadForCharacter(charKey);
            }))
            {
                frm.ShowDialog(owner);
            }
        }

        public static void OpenSelfCultivationSettings(IWin32Window owner = null, uint playerId = 0)
        {
            var bot = ResolveTargetBot(playerId);
            if (bot == null)
            {
                MessageBox.Show(owner, "Nenhum processo/personagem conectado no BotHub.", "Auto Cultivo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bot.InitializeNewModules();
            string charKey = GetCharKey(bot);
            var cfg = SelfCultivationConfig.LoadForChar(charKey);

            using (var frm = new SelfCultivationSettingsForm(charKey, cfg, () =>
            {
                cfg.SaveForChar(charKey);
                bot.Modules?.LoadForCharacter(charKey);
            }))
            {
                frm.ShowDialog(owner);
            }
        }

        public static void OpenCombatBuffSettings(IWin32Window owner = null, uint playerId = 0)
        {
            var bot = ResolveTargetBot(playerId);
            if (bot == null)
            {
                MessageBox.Show(owner, "Nenhum processo/personagem conectado no BotHub.", "Buffs / Combate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bot.InitializeNewModules();
            string charKey = GetCharKey(bot);
            var cfg = CombatBuffConfig.LoadForChar(charKey);

            using (var frm = new CombatBuffSettingsForm(charKey, cfg, () =>
            {
                cfg.SaveForChar(charKey);
                bot.Modules?.LoadForCharacter(charKey);
            }))
            {
                frm.ShowDialog(owner);
            }
        }
    }
}