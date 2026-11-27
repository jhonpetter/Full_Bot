using System;
using System.Drawing;
using System.Windows.Forms;

namespace Full_Bot
{
    public class PlayerTabControl : UserControl
    {
        public PlayerSession Session { get; }
        public BotEngine Engine { get; }

        private Label _lblSummary;
        private CheckBox _chkBotEnabled;

        private Button _btnModeSoloCultivo;
        private Button _btnModeGrupoCultivo;
        private Button _btnModeFarme;
        private Button _btnModeColetaMatts;

        private Button _btnCfgBatalhaSolo;
        private Button _btnCfgBatalhaGrupo;
        private Button _btnCfgRotasMatts;
        private Button _btnCfgAutoCultivo;
        private Button _btnCfgGeral;

        private Button _btnQuests;
        private Button _btnOnlineFriends;
        private Button _btnPartyMembers;

        private bool _syncingUi;

        public PlayerTabControl(PlayerSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Engine = new BotEngine(Session);
            Engine.EnabledChanged += OnEngineEnabledChanged;
            Engine.ModeChanged += OnEngineModeChanged;

            Dock = DockStyle.Fill;
            BuildUi();
            SyncModeButtons();
            RefreshView();
        }

        private void BuildUi()
        {
            Padding = new Padding(12);

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Linha 0: Resumo do player + Check Ativar/Desativar Bot
            var topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            _chkBotEnabled = new CheckBox
            {
                Text = "Ativar Bot",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, 4, 16, 4)
            };
            _chkBotEnabled.CheckedChanged += (s, e) =>
            {
                if (_syncingUi) return;
                Engine.SetEnabled(_chkBotEnabled.Checked);
            };

            _lblSummary = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Text = "Carregando personagem...",
                Margin = new Padding(0, 3, 0, 4)
            };

            topBar.Controls.AddRange(new Control[] { _chkBotEnabled, _lblSummary });

            // Linha 1: Seleção de Modo
            var modeBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            _btnModeSoloCultivo = CreateModeButton("Auto Cultivo Solo", BotMode.AutoCultivoSolo);
            _btnModeGrupoCultivo = CreateModeButton("Auto Cultivo em Grupo", BotMode.AutoCultivoGrupo);
            _btnModeFarme = CreateModeButton("Auto Farme", BotMode.AutoFarme);
            _btnModeColetaMatts = CreateModeButton("Auto Coleta Matts", BotMode.AutoColetaMatts);

            modeBar.Controls.AddRange(new Control[]
            {
                _btnModeSoloCultivo,
                _btnModeGrupoCultivo,
                _btnModeFarme,
                _btnModeColetaMatts
            });

            // Linha 2: Botões de Configuração
            var configBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };

            _btnCfgBatalhaSolo = CreateConfigButton("Conf Batalha Solo", BotConfigWindow.BatalhaSolo);
            _btnCfgBatalhaGrupo = CreateConfigButton("Conf Batalha Grupo", BotConfigWindow.BatalhaGrupo);
            _btnCfgRotasMatts = CreateConfigButton("Config Rotas Matts", BotConfigWindow.RotasMatts);
            _btnCfgAutoCultivo = CreateConfigButton("Config Auto Cultivo", BotConfigWindow.AutoCultivo);
            _btnCfgGeral = CreateConfigButton("Conf Geral", BotConfigWindow.Geral);

            configBar.Controls.AddRange(new Control[]
            {
                _btnCfgBatalhaSolo,
                _btnCfgBatalhaGrupo,
                _btnCfgRotasMatts,
                _btnCfgAutoCultivo,
                _btnCfgGeral
            });

            // Linha 3: Utilitários da sessão (mantidos)
            var utilBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            _btnQuests = new Button
            {
                Size = new Size(170, 30),
                Text = "Quests (Visualização)",
                Margin = new Padding(0, 0, 6, 4)
            };
            _btnQuests.Click += (s, e) => OpenQuestsForm();

            _btnOnlineFriends = new Button
            {
                Size = new Size(150, 30),
                Text = "Amigos Online",
                Margin = new Padding(0, 0, 6, 4)
            };
            _btnOnlineFriends.Click += (s, e) => OpenFriendsForm();

            _btnPartyMembers = new Button
            {
                Size = new Size(170, 30),
                Text = "Membros da Party",
                Enabled = false,
                Margin = new Padding(0, 0, 6, 4)
            };
            _btnPartyMembers.Click += (s, e) => OpenPartyForm();

            utilBar.Controls.AddRange(new Control[]
            {
                _btnQuests,
                _btnOnlineFriends,
                _btnPartyMembers
            });

            rootLayout.Controls.Add(topBar, 0, 0);
            rootLayout.Controls.Add(modeBar, 0, 1);
            rootLayout.Controls.Add(configBar, 0, 2);
            rootLayout.Controls.Add(utilBar, 0, 3);
            rootLayout.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 4);

            Controls.Add(rootLayout);
        }

        private Button CreateModeButton(string text, BotMode mode)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(6, 3, 6, 3),
                Margin = new Padding(0, 0, 6, 4)
            };
            btn.Click += (s, e) => Engine.SetMode(mode);
            return btn;
        }

        private Button CreateConfigButton(string text, BotConfigWindow window)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(6, 3, 6, 3),
                Margin = new Padding(0, 0, 6, 4)
            };
            btn.Click += (s, e) => Engine.RequestConfigWindow(window);
            return btn;
        }

        private void OnEngineEnabledChanged(bool enabled)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(OnEngineEnabledChanged), enabled);
                return;
            }

            _syncingUi = true;
            _chkBotEnabled.Checked = enabled;
            _syncingUi = false;
        }

        private void OnEngineModeChanged(BotMode mode)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<BotMode>(OnEngineModeChanged), mode);
                return;
            }

            SyncModeButtons();
        }

        private void SyncModeButtons()
        {
            SetModeButtonActive(_btnModeSoloCultivo, Engine.Mode == BotMode.AutoCultivoSolo);
            SetModeButtonActive(_btnModeGrupoCultivo, Engine.Mode == BotMode.AutoCultivoGrupo);
            SetModeButtonActive(_btnModeFarme, Engine.Mode == BotMode.AutoFarme);
            SetModeButtonActive(_btnModeColetaMatts, Engine.Mode == BotMode.AutoColetaMatts);
        }

        private static void SetModeButtonActive(Button btn, bool active)
        {
            btn.BackColor = active ? Color.SteelBlue : SystemColors.Control;
            btn.ForeColor = active ? Color.White : SystemColors.ControlText;
            btn.Font = new Font(btn.Font, active ? FontStyle.Bold : FontStyle.Regular);
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