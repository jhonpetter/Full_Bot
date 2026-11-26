using System;
using System.Drawing;
using System.Windows.Forms;

namespace Full_Bot
{
    public class PlayerTabControl : UserControl
    {
        public PlayerSession Session { get; }

        private Label _lblSummary;
        private Button _btnQuests;
        private Button _btnOnlineFriends;
        private Button _btnPartyMembers;
        // [TEMP_TEST_FORM] Remover/comentar quando terminar os testes
        private Button _btnTestFunctions;

        public PlayerTabControl(PlayerSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Dock = DockStyle.Fill;
            BuildUi();
            RefreshView();
        }

        private void BuildUi()
        {
            Padding = new Padding(16);

            _lblSummary = new Label
            {
                Location = new Point(16, 16),
                Size = new Size(680, 28),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Text = "Carregando personagem..."
            };

            _btnQuests = new Button
            {
                Location = new Point(16, 60),
                Size = new Size(190, 34),
                Text = "Quests (Visualização)"
            };
            _btnQuests.Click += (s, e) => OpenQuestsForm();

            _btnOnlineFriends = new Button
            {
                Location = new Point(216, 60),
                Size = new Size(190, 34),
                Text = "Amigos Online"
            };
            _btnOnlineFriends.Click += (s, e) => OpenFriendsForm();

            _btnPartyMembers = new Button
            {
                Location = new Point(416, 60),
                Size = new Size(190, 34),
                Text = "Membros da Party",
                Enabled = false
            };
            _btnPartyMembers.Click += (s, e) => OpenPartyForm();

            // [TEMP_TEST_FORM] Botão temporário para abrir o form de teste das funções
            _btnTestFunctions = new Button
            {
                Location = new Point(616, 60),
                Size = new Size(165, 34),
                Text = "Testar Funções"
            };
            _btnTestFunctions.Click += (s, e) => OpenTestFunctionsForm();

            Controls.AddRange(new Control[]
            {
                _lblSummary,
                _btnQuests,
                _btnOnlineFriends,
                _btnPartyMembers,
                // [TEMP_TEST_FORM]
                _btnTestFunctions
            });
        }

        // [TEMP_TEST_FORM] Remover/comentar quando terminar os testes
        private void OpenTestFunctionsForm()
        {
            var p = Session?.Player;
            if (p == null)
            {
                MessageBox.Show("PlayerCore não disponível na sessão atual.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var testFrm = new PlayerFunctionsTestForm(p);
            testFrm.Show(this);
        }

        public void RefreshView()
        {
            var p = Session?.Player;
            if (p == null) return;

            _lblSummary.Text = $"{p.Name} (ID: {p.Id})  |  Nv. {p.Level}  |  HP: {p.HP}/{p.HPMax}  |  MP: {p.MP}/{p.MPMax}";

            bool inParty = p.IsInParty();
            int partyCount = inParty ? p.GetPartyMembers().Count : 0;
            _btnPartyMembers.Enabled = inParty;
            _btnPartyMembers.Text = inParty ? $"Membros da Party ({partyCount})" : "Membros da Party";
        }

        private void OpenQuestsForm()
        {
            var viewer = new QuestViewerForm(Session);
            viewer.Show(this);
        }

        private void OpenFriendsForm()
        {
            var frm = new Form
            {
                Text = $"Amigos - {Session.Player.Name}",
                Size = new Size(520, 320),
                StartPosition = FormStartPosition.CenterParent
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.Columns.Add("Id", "ID");
            grid.Columns.Add("Name", "Nome");
            grid.Columns.Add("Level", "Nível");
            grid.Columns.Add("ClassId", "Classe");

            foreach (var f in Session.Player.GetFriends())
                grid.Rows.Add(f.Id, f.Name, f.Level, f.ClassId);

            frm.Controls.Add(grid);
            frm.Show(this);
        }

        private void OpenPartyForm()
        {
            var frm = new Form
            {
                Text = $"Party - {Session.Player.Name}",
                Size = new Size(640, 320),
                StartPosition = FormStartPosition.CenterParent
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.Columns.Add("Slot", "Slot");
            grid.Columns.Add("Id", "ID");
            grid.Columns.Add("Level", "Nível");
            grid.Columns.Add("HP", "HP");
            grid.Columns.Add("MP", "MP");
            grid.Columns.Add("Buffs", "Buffs");

            void ReloadPartyGrid()
            {
                if (grid.IsDisposed) return;
                int firstRow = grid.FirstDisplayedScrollingRowIndex;
                int selRow = grid.CurrentRow?.Index ?? -1;

                grid.Rows.Clear();
                foreach (var m in Session.Player.GetPartyMembers())
                {
                    string buffsStr = (m.BuffIconIds != null && m.BuffIconIds.Count > 0)
                        ? string.Join(", ", m.BuffIconIds)
                        : "-";

                    grid.Rows.Add(m.Index + 1, m.Id, m.Level, $"{m.HP}/{m.HPMax}", $"{m.MP}/{m.MPMax}", buffsStr);
                }

                if (firstRow >= 0 && firstRow < grid.Rows.Count)
                    grid.FirstDisplayedScrollingRowIndex = firstRow;
                if (selRow >= 0 && selRow < grid.Rows.Count)
                    grid.Rows[selRow].Selected = true;
            }

            ReloadPartyGrid();

            var timer = new Timer { Interval = 1000, Enabled = true };
            timer.Tick += (s, e) => ReloadPartyGrid();
            frm.FormClosed += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
            };

            frm.Controls.Add(grid);
            frm.Show(this);
        }
    }
}