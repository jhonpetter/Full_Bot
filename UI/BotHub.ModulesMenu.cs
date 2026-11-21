using System;
using System.Drawing;
using System.Windows.Forms;

namespace Full_Bot
{
    public static class BotHubModulesMenu
    {
        public static ToolStrip CreateModulesToolStrip(Func<uint> getSelectedPlayerId = null, IWin32Window owner = null)
        {
            var strip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(4, 2, 4, 2)
            };

            var btnWhoAmI = new ToolStripButton("WhoAmI (Papel/PT)")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnWhoAmI.Click += (s, e) =>
            {
                uint pid = getSelectedPlayerId != null ? getSelectedPlayerId() : 0u;
                BotEngineModulesUiHelper.OpenWhoAmISettings(owner ?? strip.FindForm(), pid);
            };

            var btnCultivation = new ToolStripButton("Auto Cultivo")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnCultivation.Click += (s, e) =>
            {
                uint pid = getSelectedPlayerId != null ? getSelectedPlayerId() : 0u;
                BotEngineModulesUiHelper.OpenSelfCultivationSettings(owner ?? strip.FindForm(), pid);
            };

            var btnCombatBuffs = new ToolStripButton("Buffs / Combate")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            btnCombatBuffs.Click += (s, e) =>
            {
                uint pid = getSelectedPlayerId != null ? getSelectedPlayerId() : 0u;
                BotEngineModulesUiHelper.OpenCombatBuffSettings(owner ?? strip.FindForm(), pid);
            };

            var btnDungeonToggle = new ToolStripButton("Modo: Mundo Aberto")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                CheckOnClick = true
            };
            btnDungeonToggle.CheckedChanged += (s, e) =>
            {
                bool isDg = btnDungeonToggle.Checked;
                btnDungeonToggle.Text = isDg ? "Modo: DG / Boss" : "Modo: Mundo Aberto";
                uint pid = getSelectedPlayerId != null ? getSelectedPlayerId() : 0u;
                var bot = BotEngineModulesUiHelper.ResolveTargetBot(pid);
                if (bot != null)
                    bot.IsInDungeon = isDg;
            };

            var btnRefreshProcs = new ToolStripButton("Atualizar Clients")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            btnRefreshProcs.Click += (s, e) =>
            {
                int added = BotHub.RefreshProcesses();
                var host = owner ?? strip.FindForm();
                MessageBox.Show(host, $"Clients novos conectados: {added}", "BotHub", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            strip.Items.AddRange(new ToolStripItem[]
            {
                btnWhoAmI,
                btnCultivation,
                btnCombatBuffs,
                new ToolStripSeparator(),
                btnDungeonToggle,
                btnRefreshProcs
            });

            return strip;
        }
    }
}