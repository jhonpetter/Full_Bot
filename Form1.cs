using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Full_Bot
{
    public partial class Form1 : Form
    {
        private const string ConfigJsonPath = @"Config\config.CT";
        private readonly Dictionary<int, TabPage> _clientTabs = new Dictionary<int, TabPage>();
        private readonly Dictionary<int, ClientPanel> _clientPanels = new Dictionary<int, ClientPanel>();
        private ToolStrip _modulesStrip;

        public Form1()
        {
            InitializeComponent();
            AttachModulesStrip();
            SyncMultiClients();
            BotHub.PulseAll();
            RefreshOverviewAndPanels();
        }

        private void AttachModulesStrip()
        {
            if (_modulesStrip != null) return;

            _modulesStrip = BotHub.CreateDefaultModulesStrip(() =>
            {
                if (tabClients.SelectedTab != null && tabClients.SelectedTab.Tag is int selectedPid)
                {
                    if (_clientPanels.TryGetValue(selectedPid, out var panel))
                        return panel?.Bot?.Player?.Id ?? 0u;
                }
                return 0u;
            }, this);

            _modulesStrip.Dock = DockStyle.Top;
            Controls.Add(_modulesStrip);
        }

        private void tmrUpdate_Tick(object sender, EventArgs e)
        {
            try
            {
                SyncMultiClients();
                BotHub.PulseAll();
                RefreshOverviewAndPanels();
            }
            catch
            {
                // Evita travar o timer da UI em falhas transitórias de leitura
            }
        }

        private void SyncMultiClients()
        {
            BotHub.RefreshProcesses(ConfigJsonPath);
            var snapshot = BotHub.GetSnapshot();
            var activePids = new HashSet<int>(
                snapshot
                    .Where(b => b?.Memory?.Process != null && !b.Memory.Process.HasExited)
                    .Select(b => b.Memory.Process.Id));

            var closedPids = _clientTabs.Keys.Where(pid => !activePids.Contains(pid)).ToList();
            foreach (int pid in closedPids)
            {
                if (_clientTabs.TryGetValue(pid, out var tab))
                {
                    if (tabClients.TabPages.Contains(tab))
                        tabClients.TabPages.Remove(tab);
                    tab.Dispose();
                }
                _clientTabs.Remove(pid);
                _clientPanels.Remove(pid);
            }

            foreach (var bot in snapshot)
            {
                if (bot?.Memory?.Process == null || bot.Player == null) continue;
                int pid = bot.Memory.Process.Id;
                if (_clientTabs.ContainsKey(pid)) continue;

                var panel = new ClientPanel(bot)
                {
                    Dock = DockStyle.Fill
                };

                string title = string.IsNullOrWhiteSpace(bot.Player.Name) || bot.Player.Name.StartsWith("PID:")
                    ? $"PID {pid}"
                    : $"{bot.Player.Name} ({pid})";

                var tabPage = new TabPage(title)
                {
                    Tag = pid
                };
                tabPage.Controls.Add(panel);

                tabClients.TabPages.Add(tabPage);
                _clientTabs[pid] = tabPage;
                _clientPanels[pid] = panel;
            }
        }

        private void RefreshOverviewAndPanels()
        {
            var snapshot = BotHub.GetSnapshot();

            int firstRow = gridOverview.FirstDisplayedScrollingRowIndex;
            int selectedRow = gridOverview.CurrentRow != null ? gridOverview.CurrentRow.Index : -1;

            gridOverview.Rows.Clear();
            foreach (var bot in snapshot)
            {
                if (bot?.Memory?.Process == null) continue;
                var p = bot.Player;
                string posText = p != null
                    ? $"{PlayerCore.ToDisplayX(p.X):F0}, {PlayerCore.ToDisplayZ(p.Z):F0}"
                    : "-";

                gridOverview.Rows.Add(
                    p != null ? p.Name : $"PID {bot.Memory.Process.Id}",
                    p != null ? p.Level.ToString() : "-",
                    p != null ? $"{p.HP}/{p.HPMax}" : "-",
                    p != null ? $"{p.MP}/{p.MPMax}" : "-",
                    posText
                );
            }

            if (firstRow >= 0 && firstRow < gridOverview.Rows.Count)
                gridOverview.FirstDisplayedScrollingRowIndex = firstRow;

            if (selectedRow >= 0 && selectedRow < gridOverview.Rows.Count)
                gridOverview.Rows[selectedRow].Selected = true;

            foreach (var kv in _clientPanels)
            {
                int pid = kv.Key;
                var panel = kv.Value;
                var bot = panel?.Bot;

                if (_clientTabs.TryGetValue(pid, out var tab) && bot?.Player != null)
                {
                    string charName = bot.Player.Name;
                    string desiredTitle = string.IsNullOrWhiteSpace(charName) || charName.StartsWith("PID:")
                        ? $"PID {pid}"
                        : $"{charName} ({pid})";

                    if (tab.Text != desiredTitle)
                        tab.Text = desiredTitle;
                }

                panel?.UpdateTick();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            tmrUpdate.Stop();
            BotHub.ClearAll();
            base.OnFormClosed(e);
        }
    }
}