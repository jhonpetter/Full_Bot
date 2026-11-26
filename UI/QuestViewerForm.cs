using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Full_Bot
{
    public class QuestViewerForm : Form
    {
        private readonly PlayerSession _session;
        private readonly Timer _refreshTimer;

        private TabControl _tabQuests;
        private TreeView _treeActive;
        private DataGridView _gridNews;
        private Button _btnRefreshNow;

        public QuestViewerForm(PlayerSession session)
        {
            _session = session;
            var p = _session?.Player;
            Text = p != null
                ? $"Missões - {p.Name} (Nv. {p.Level})"
                : "Missões";
            Size = new Size(760, 460);
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            RefreshData();

            _refreshTimer = new Timer { Interval = 1500, Enabled = true };
            _refreshTimer.Tick += (s, e) => RefreshData();
            FormClosed += (s, e) => { _refreshTimer.Stop(); _refreshTimer.Dispose(); };
        }

        private void BuildUI()
        {
            _btnRefreshNow = new Button
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = "Atualizar Listas de Quests Agora",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _btnRefreshNow.Click += (s, e) => RefreshData();

            _tabQuests = new TabControl { Dock = DockStyle.Fill };

            TabPage tabActive = new TabPage("Quests Ativas (Cascata)");
            _treeActive = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                FullRowSelect = true
            };
            tabActive.Controls.Add(_treeActive);

            TabPage tabNews = new TabPage("Quests Disponíveis (Journal)");
            _gridNews = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _gridNews.Columns.Add("QuestId", "ID Quest");
            _gridNews.Columns.Add("Name", "Nome da Missão");
            _gridNews.Columns.Add("ReqLvl", "Nível Min.");
            _gridNews.Columns.Add("NpcFrom", "NPC Pegar");
            _gridNews.Columns.Add("NpcTo", "NPC Entregar");
            tabNews.Controls.Add(_gridNews);

            _tabQuests.TabPages.Add(tabActive);
            _tabQuests.TabPages.Add(tabNews);

            Controls.Add(_tabQuests);
            Controls.Add(_btnRefreshNow);
        }

        private void RefreshData()
        {
            if (_session == null || !_session.IsAlive || _session.Player == null || _session.Player.Base == 0)
                return;

            var questRoots = _session.Player.GetActiveQuests();

            var collapsedIds = new HashSet<string>();
            SaveExpandedState(_treeActive.Nodes, collapsedIds);
            string topNodeTag = _treeActive.TopNode?.Tag as string;

            _treeActive.BeginUpdate();
            _treeActive.Nodes.Clear();

            foreach (var root in questRoots)
                _treeActive.Nodes.Add(BuildQuestNode(root));

            RestoreExpandedState(_treeActive.Nodes, collapsedIds);

            if (!string.IsNullOrEmpty(topNodeTag))
            {
                var foundTop = FindNodeByTag(_treeActive.Nodes, topNodeTag);
                if (foundTop != null)
                    _treeActive.TopNode = foundTop;
            }
            _treeActive.EndUpdate();

            int firstDisplayedRow = _gridNews.FirstDisplayedScrollingRowIndex;
            int selectedRow = _gridNews.CurrentRow?.Index ?? -1;

            var news = _session.Player.GetNewsQuests();
            _gridNews.Rows.Clear();

            foreach (var nq in news)
            {
                _gridNews.Rows.Add(
                    nq.QuestId,
                    string.IsNullOrWhiteSpace(nq.Name) ? $"Quest #{nq.QuestId}" : nq.Name,
                    nq.RequiredLevel,
                    nq.FromNpcId,
                    nq.ToNpcId
                );
            }

            if (firstDisplayedRow >= 0 && firstDisplayedRow < _gridNews.Rows.Count)
                _gridNews.FirstDisplayedScrollingRowIndex = firstDisplayedRow;

            if (selectedRow >= 0 && selectedRow < _gridNews.Rows.Count)
                _gridNews.Rows[selectedRow].Selected = true;
        }

        private TreeNode BuildQuestNode(ActiveQuestInfo q)
        {
            string title = string.IsNullOrWhiteSpace(q.Name) ? $"Quest #{q.QuestId}" : q.Name;
            string statusTag = q.Status == 3 ? " [PRONTA P/ ENTREGAR]" : (q.Status == 2 ? " [Em Andamento]" : "");

            var node = new TreeNode($"[{q.QuestId}] {title}{statusTag}")
            {
                Tag = $"Q_{q.QuestId}_{q.SlotIndex}"
            };

            if (q.Status == 3)
                node.ForeColor = Color.DarkGreen;

            foreach (var t in q.Targets ?? new List<QuestTargetRequirement>())
            {
                if (t.MobId > 0)
                    node.Nodes.Add(new TreeNode($"Objetivo: Matar Mob ID {t.MobId} -> {t.MobsKilled} / {t.MobsRequired}")
                    {
                        ForeColor = Color.DarkRed,
                        Tag = $"T_{q.QuestId}_M_{t.MobId}"
                    });

                if (t.ItemId > 0)
                    node.Nodes.Add(new TreeNode($"Objetivo: Coletar Item ID {t.ItemId} -> {t.ItemsCollected} / {t.ItemsRequired}")
                    {
                        ForeColor = Color.DarkBlue,
                        Tag = $"T_{q.QuestId}_I_{t.ItemId}"
                    });
            }

            if (q.ToNpcId > 0)
                node.Nodes.Add(new TreeNode($"Entregar no NPC ID: {q.ToNpcId}")
                {
                    ForeColor = Color.DimGray,
                    Tag = $"NPC_{q.QuestId}_{q.ToNpcId}"
                });

            foreach (var child in q.Children ?? new List<ActiveQuestInfo>())
                node.Nodes.Add(BuildQuestNode(child));

            return node;
        }

        private void SaveExpandedState(TreeNodeCollection nodes, HashSet<string> collapsed)
        {
            foreach (TreeNode n in nodes)
            {
                if (n.Nodes.Count > 0 && !n.IsExpanded && n.Tag is string tag)
                    collapsed.Add(tag);
                SaveExpandedState(n.Nodes, collapsed);
            }
        }

        private void RestoreExpandedState(TreeNodeCollection nodes, HashSet<string> collapsed)
        {
            foreach (TreeNode n in nodes)
            {
                if (n.Nodes.Count > 0)
                {
                    if (n.Tag is string tag && collapsed.Contains(tag))
                        n.Collapse();
                    else
                        n.Expand();
                }
                RestoreExpandedState(n.Nodes, collapsed);
            }
        }

        private TreeNode FindNodeByTag(TreeNodeCollection nodes, string tag)
        {
            foreach (TreeNode n in nodes)
            {
                if (n.Tag as string == tag) return n;
                var child = FindNodeByTag(n.Nodes, tag);
                if (child != null) return child;
            }
            return null;
        }
    }
}