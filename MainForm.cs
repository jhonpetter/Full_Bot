using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Full_Bot
{
    public partial class MainForm : Form
    {
        private readonly Dictionary<int, TabPage> _playerTabs = new Dictionary<int, TabPage>();
        private readonly Dictionary<int, PlayerTabControl> _playerTabControls = new Dictionary<int, PlayerTabControl>();

        public MainForm()
        {
            InitializeComponent();
            SyncProcessesAndTabs();
            RefreshGeneralInfoAndTabs();
        }

        private void tmrSync_Tick(object sender, EventArgs e)
        {
            try
            {
                SyncProcessesAndTabs();
                RefreshGeneralInfoAndTabs();
            }
            catch
            {
                // Mantém a UI viva em leituras transitórias
            }
        }

        private static string GetCharTabTitle(PlayerSession session)
        {
            int pid = session?.Memory?.Process?.Id ?? 0;
            string name = session?.Player?.Name;

            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("PID:", StringComparison.OrdinalIgnoreCase))
                return $"PID {pid}";

            return name.Trim();
        }

        private void SyncProcessesAndTabs()
        {
            BotHub.RefreshProcesses(BotHub.DefaultConfigPath);
            var snapshot = BotHub.GetSnapshot();

            var activePids = new HashSet<int>(
                snapshot
                    .Where(s => s != null && s.IsAlive)
                    .Select(s => s.Memory.Process.Id));

            var closedPids = _playerTabs.Keys.Where(pid => !activePids.Contains(pid)).ToList();
            foreach (int pid in closedPids)
            {
                if (_playerTabs.TryGetValue(pid, out var tab))
                {
                    if (tabMain.TabPages.Contains(tab))
                        tabMain.TabPages.Remove(tab);
                    tab.Dispose();
                }

                _playerTabs.Remove(pid);
                _playerTabControls.Remove(pid);
            }

            foreach (var session in snapshot)
            {
                if (session == null || !session.IsAlive || session.Player == null) continue;
                int pid = session.Memory.Process.Id;
                if (_playerTabs.ContainsKey(pid)) continue;

                var playerControl = new PlayerTabControl(session)
                {
                    Dock = DockStyle.Fill
                };

                var tabPage = new TabPage(GetCharTabTitle(session))
                {
                    Tag = pid
                };
                tabPage.Controls.Add(playerControl);

                tabMain.TabPages.Add(tabPage);
                _playerTabs[pid] = tabPage;
                _playerTabControls[pid] = playerControl;
            }
        }

        private void RefreshGeneralInfoAndTabs()
        {
            this.Text = $"Full_Bot - {BotHub.LastStatus}";

            var snapshot = BotHub.GetSnapshot();

            int firstRow = gridGeneral.FirstDisplayedScrollingRowIndex;
            int selectedRow = gridGeneral.CurrentRow != null ? gridGeneral.CurrentRow.Index : -1;

            gridGeneral.Rows.Clear();
            foreach (var session in snapshot)
            {
                if (session == null || !session.IsAlive) continue;
                var p = session.Player;

                gridGeneral.Rows.Add(
                    session.Memory.Process.Id,
                    p != null ? p.Name : $"PID {session.Memory.Process.Id}",
                    p != null ? p.Level.ToString() : "-",
                    p != null ? $"{p.HP}/{p.HPMax}" : "-",
                    p != null ? $"{p.MP}/{p.MPMax}" : "-",
                    p != null && !string.IsNullOrWhiteSpace(p.CurrentRegionName) ? p.CurrentRegionName : "-"
                );
            }

            if (firstRow >= 0 && firstRow < gridGeneral.Rows.Count)
                gridGeneral.FirstDisplayedScrollingRowIndex = firstRow;

            if (selectedRow >= 0 && selectedRow < gridGeneral.Rows.Count)
                gridGeneral.Rows[selectedRow].Selected = true;

            foreach (var kv in _playerTabControls)
            {
                int pid = kv.Key;
                var ctrl = kv.Value;
                var session = ctrl?.Session;

                if (_playerTabs.TryGetValue(pid, out var tab) && session != null)
                {
                    string desiredTitle = GetCharTabTitle(session);
                    if (tab.Text != desiredTitle)
                        tab.Text = desiredTitle;
                }

                ctrl?.RefreshView();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            tmrSync.Stop();
            BotHub.ClearAll();
            base.OnFormClosed(e);
        }
    }
}