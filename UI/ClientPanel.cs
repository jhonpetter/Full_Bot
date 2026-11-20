using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Full_Bot
{
    public class ClientPanel : UserControl
    {
        public BotController Bot { get; private set; }
        private CharacterProfile _profile;
        private string _loadedProfileChar = string.Empty;
        private bool _isPulsing = false;

        private Label lblStats, lblPos, lblBuffs, lblStatus;
        private CheckBox chkEnabled, chkAutoParty, chkAutoInviteFriends, chkHelpAllies;
        private ComboBox cmbRole, cmbLeader, cmbLootMode;
        private TextBox txtAllowedLeaders, txtInviteFriends, txtMaxLeaderDist;
        private TextBox txtBuffRules, txtAttackSkills, txtFarmMobs, txtGatherMatts, txtLootRadius;
        private Button btnSaveApply, btnOpenJson, btnReloadJson, btnInviteOthers, btnQuests;

        public ClientPanel(BotController bot)
        {
            Bot = bot;
            this.Dock = DockStyle.Fill;
            this.AutoScroll = true;
            this.AutoScrollMinSize = new Size(520, 510);

            BuildLayout();
            LoadCharProfile(Bot.Player.Name);
        }

        private void BuildLayout()
        {
            this.Padding = new Padding(10);

            lblStats = new Label { Location = new Point(12, 12), Size = new Size(490, 20), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Text = "HP: 0/0 | MP: 0/0" };
            lblPos = new Label { Location = new Point(12, 35), Size = new Size(490, 20), Text = "Posição: 0, 0" };
            lblBuffs = new Label { Location = new Point(12, 58), Size = new Size(490, 20), Text = "Buffs Ativos: Nenhum" };
            lblStatus = new Label { Location = new Point(12, 82), Size = new Size(490, 22), ForeColor = Color.DarkBlue, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Text = "Estado: Parado" };

            chkEnabled = new CheckBox { Location = new Point(12, 112), Size = new Size(115, 24), Text = "LIGAR BOT", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.DarkGreen };
            chkEnabled.CheckedChanged += (s, e) => { Bot.IsEnabled = chkEnabled.Checked; };

            chkAutoParty = new CheckBox { Location = new Point(135, 112), Size = new Size(135, 24), Text = "Auto-Aceitar PT", Checked = true };
            chkAutoInviteFriends = new CheckBox { Location = new Point(275, 112), Size = new Size(155, 24), Text = "Auto-Chamar Amigos", Checked = false };
            chkHelpAllies = new CheckBox { Location = new Point(12, 138), Size = new Size(185, 24), Text = "Ajudar Quests dos Aliados", Checked = true };

            // Botões de Perfil Exclusivo (.json)
            btnOpenJson = new Button { Location = new Point(12, 168), Size = new Size(150, 26), Text = "Abrir Config do Char" };
            btnOpenJson.Click += (s, e) =>
            {
                string path = ProfileManager.GetProfilePath(Bot.Player.Name);
                Process.Start("notepad.exe", path);
            };

            btnReloadJson = new Button { Location = new Point(170, 168), Size = new Size(135, 26), Text = "Recarregar JSON" };
            btnReloadJson.Click += (s, e) => LoadCharProfile(Bot.Player.Name, forceReload: true);

            btnInviteOthers = new Button { Location = new Point(315, 168), Size = new Size(153, 26), Text = "Chamar Bots Agora" };
            btnInviteOthers.Click += (s, e) =>
            {
                foreach (var other in BotHub.GetSnapshot())
                {
                    if (other.Player.Id != 0 && other.Player.Id != Bot.Player.Id)
                        Bot.Party.Invite(other.Player.Id);
                }
            };

            // Modo Grupo / Solo + Raio Máximo
            var lblRole = new Label { Location = new Point(12, 206), Size = new Size(85, 20), Text = "Modo:" };
            cmbRole = new ComboBox { Location = new Point(185, 203), Size = new Size(125, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRole.Items.AddRange(new object[] { "Solo", "Líder", "Seguidor" });
            cmbRole.SelectedIndex = 0;

            cmbLeader = new ComboBox { Location = new Point(318, 203), Size = new Size(150, 24), DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            cmbRole.SelectedIndexChanged += (s, e) =>
            {
                cmbLeader.Enabled = (cmbRole.SelectedIndex == 2);
                RefreshLeaderList();
            };

            var lMaxDist = new Label { Location = new Point(12, 236), Size = new Size(170, 20), Text = "Raio Máx. Líder (m):" };
            txtMaxLeaderDist = new TextBox { Location = new Point(185, 233), Size = new Size(80, 22), Text = "100" };

            // Listas de Grupo (Aceitar PT e Chamar Amigos)
            var lPt = new Label { Location = new Point(12, 266), Size = new Size(170, 20), Text = "Aceitar PT só IDs:" };
            txtAllowedLeaders = new TextBox { Location = new Point(185, 263), Size = new Size(283, 22) };

            var lInv = new Label { Location = new Point(12, 296), Size = new Size(170, 20), Text = "IDs Amigos p/ Chamar:" };
            txtInviteFriends = new TextBox { Location = new Point(185, 293), Size = new Size(283, 22) };

            // Configuração de Loot (Apenas itens próximos ao char)
            var lLoot = new Label { Location = new Point(12, 326), Size = new Size(170, 20), Text = "Loot Pós-Combate:" };
            cmbLootMode = new ComboBox { Location = new Point(185, 323), Size = new Size(175, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLootMode.Items.AddRange(new object[] { "Nenhum", "Somente Moedas", "Lista Específica (IDs)", "Todos no Raio" });
            cmbLootMode.SelectedIndex = 1;

            var lRad = new Label { Location = new Point(368, 326), Size = new Size(42, 20), Text = "Raio:" };
            txtLootRadius = new TextBox { Location = new Point(412, 323), Size = new Size(56, 22), Text = "6" };

            var lMatts = new Label { Location = new Point(12, 356), Size = new Size(170, 20), Text = "Itens Permitidos (IDs):" };
            txtGatherMatts = new TextBox { Location = new Point(185, 353), Size = new Size(283, 22) };

            // Buffs, Skills e Mobs
            var l1 = new Label { Location = new Point(12, 386), Size = new Size(170, 20), Text = "Auto-Buff (IconId:SkillId):" };
            txtBuffRules = new TextBox { Location = new Point(185, 383), Size = new Size(283, 22) };

            var l2 = new Label { Location = new Point(12, 416), Size = new Size(170, 20), Text = "Skills Batalha (IDs):" };
            txtAttackSkills = new TextBox { Location = new Point(185, 413), Size = new Size(283, 22) };

            var l3 = new Label { Location = new Point(12, 446), Size = new Size(170, 20), Text = "Farmar Mobs (IDs):" };
            txtFarmMobs = new TextBox { Location = new Point(185, 443), Size = new Size(283, 22) };

            btnSaveApply = new Button { Location = new Point(185, 476), Size = new Size(283, 30), Text = "Salvar e Aplicar no Perfil", Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            btnSaveApply.Click += (s, e) => SaveAndApplySettings();

            this.AutoScrollMinSize = new Size(520, 550);

            btnQuests = new Button
            {
                Location = new Point(185, 512),
                Size = new Size(283, 30),
                Text = "Ver Quests (Ativas e Disponíveis)",
                BackColor = Color.LightSteelBlue,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnQuests.Click += (s, e) =>
            {
                var questForm = new QuestViewerForm(Bot);
                questForm.Show(this);
            };

            this.Controls.AddRange(new Control[] {
                lblStats, lblPos, lblBuffs, lblStatus, btnQuests,
                chkEnabled, chkAutoParty, chkAutoInviteFriends, chkHelpAllies,
                btnOpenJson, btnReloadJson, btnInviteOthers,
                lblRole, cmbRole, cmbLeader,
                lMaxDist, txtMaxLeaderDist,
                lPt, txtAllowedLeaders,
                lInv, txtInviteFriends,
                lLoot, cmbLootMode, lRad, txtLootRadius,
                lMatts, txtGatherMatts,
                l1, txtBuffRules, l2, txtAttackSkills, l3, txtFarmMobs, btnSaveApply
            });
        }

        public void LoadCharProfile(string charName, bool forceReload = false)
        {
            if (!forceReload && _loadedProfileChar == charName) return;

            _loadedProfileChar = charName;
            _profile = ProfileManager.LoadForChar(charName);

            chkAutoParty.Checked = _profile.AutoAcceptParty;
            chkAutoInviteFriends.Checked = _profile.AutoInviteFriends;
            chkHelpAllies.Checked = _profile.HelpAlliesInQuest;

            cmbRole.SelectedIndex = _profile.Role == "Lider" ? 1 : (_profile.Role == "Seguidor" ? 2 : 0);
            txtMaxLeaderDist.Text = _profile.MaxLeaderDistance.ToString();

            cmbLootMode.SelectedIndex = (int)_profile.LootMode;
            txtLootRadius.Text = _profile.LootRadius.ToString();

            txtAllowedLeaders.Text = string.Join(", ", _profile.AllowedPartyLeaderIds);
            txtInviteFriends.Text = string.Join(", ", _profile.AutoInviteFriendIds);
            txtGatherMatts.Text = string.Join(", ", _profile.CollectItemIds);
            txtBuffRules.Text = string.Join(", ", _profile.BuffRules.Select(b => $"{b.IconId}:{b.SkillId}"));
            txtAttackSkills.Text = string.Join(", ", _profile.AttackSkills);
            txtFarmMobs.Text = string.Join(", ", _profile.FarmMobIds);

            ApplyProfileToEngine();
        }

        public void SaveAndApplySettings()
        {
            _profile.AutoAcceptParty = chkAutoParty.Checked;
            _profile.AutoInviteFriends = chkAutoInviteFriends.Checked;
            _profile.HelpAlliesInQuest = chkHelpAllies.Checked;

            _profile.Role = cmbRole.SelectedIndex == 1 ? "Lider" : (cmbRole.SelectedIndex == 2 ? "Seguidor" : "Solo");
            _profile.LeaderPlayerId = (cmbRole.SelectedIndex == 2 && cmbLeader.SelectedItem is LeaderOption opt) ? opt.Id : 0;
            _profile.MaxLeaderDistance = float.TryParse(txtMaxLeaderDist.Text, out float maxD) ? maxD : 100f;

            _profile.LootMode = (LootFilterMode)cmbLootMode.SelectedIndex;
            _profile.LootRadius = float.TryParse(txtLootRadius.Text, out float r) ? r : 6f;
            _profile.AllowedPartyLeaderIds = ParseIdList(txtAllowedLeaders.Text);
            _profile.AutoInviteFriendIds = ParseIdList(txtInviteFriends.Text);
            _profile.CollectItemIds = ParseIdList(txtGatherMatts.Text);
            _profile.AttackSkills = ParseIdList(txtAttackSkills.Text);
            _profile.FarmMobIds = ParseIdList(txtFarmMobs.Text);
            _profile.BuffRules.Clear();
            foreach (var pair in txtBuffRules.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Trim().Split(':');
                if (parts.Length == 2 && ushort.TryParse(parts.First(), out ushort icon) && uint.TryParse(parts.Last(), out uint skill))
                    _profile.BuffRules.Add(new AutoBuffRule { IconId = icon, SkillId = skill, BuffPartyAllies = true });
            }
            ProfileManager.SaveForChar(Bot.Player.Name, _profile);
            ApplyProfileToEngine();
            MessageBox.Show($"Perfil de '{Bot.Player.Name}' salvo e aplicado!");
        }
        private void ApplyProfileToEngine()
        {
            Bot.Support.AutoAcceptParty = _profile.AutoAcceptParty;
            Bot.Support.AllowedPartyLeaderIds.Clear();
            Bot.Support.AllowedPartyLeaderIds.AddRange(_profile.AllowedPartyLeaderIds);
            Bot.Support.AutoInviteFriends = _profile.AutoInviteFriends;
            Bot.Support.AutoInviteFriendIds.Clear();
            Bot.Support.AutoInviteFriendIds.AddRange(_profile.AutoInviteFriendIds);
            Bot.Support.IsLeader = (_profile.Role == "Lider");
            Bot.Support.LeaderPlayerId = _profile.Role == "Seguidor" ? _profile.LeaderPlayerId : 0;
            Bot.Support.MaxLeaderDistance = _profile.MaxLeaderDistance;
            Bot.Support.BuffRules.Clear();
            Bot.Support.BuffRules.AddRange(_profile.BuffRules);
            Bot.Combat.HelpAlliesInQuest = _profile.HelpAlliesInQuest;
            Bot.Combat.AttackSkillRotation.Clear();
            Bot.Combat.AttackSkillRotation.AddRange(_profile.AttackSkills);
            Bot.Combat.FarmMobTemplateIds.Clear();
            Bot.Combat.FarmMobTemplateIds.AddRange(_profile.FarmMobIds);
            Bot.Gather.Mode = _profile.LootMode;
            Bot.Gather.LootRadius = _profile.LootRadius;
            Bot.Gather.CollectItemIds.Clear();
            Bot.Gather.CollectItemIds.AddRange(_profile.CollectItemIds);
        }
        private List<uint> ParseIdList(string raw)
        {
            var list = new List<uint>();
            foreach (var item in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (uint.TryParse(item.Trim(), out uint id))
                    list.Add(id);
            }
            return list;
        }
        private void RefreshLeaderList()
        {
            cmbLeader.Items.Clear();
            foreach (var b in BotHub.GetSnapshot())
            {
                if (b.Player.Id != Bot.Player.Id && b.Player.Id != 0)
                    cmbLeader.Items.Add(new LeaderOption { Id = b.Player.Id, Name = b.Player.Name });
            }
            if (cmbLeader.Items.Count > 0) cmbLeader.SelectedIndex = 0;
        }
        public void UpdateTick()
        {
            if (Bot.Memory.Process.HasExited) return;
            var p = Bot.Player;
            string currentName = p.Name;
            if (_loadedProfileChar != currentName && !currentName.StartsWith("PID:"))
                LoadCharProfile(currentName);
            lblStats.Text = $"{currentName} (Nv. {p.Level})  |  HP: {p.HP}/{p.HPMax}  |  MP: {p.MP}/{p.MPMax}";
            lblPos.Text = $"Mapa: {PlayerCore.ToDisplayX(p.X):F1}, {PlayerCore.ToDisplayZ(p.Z):F1} (Alt: {PlayerCore.ToDisplayY(p.Y):F1})  |  Região: {p.CurrentRegionName}";
            var buffs = Bot.Buffs.GetActiveBuffs();
            lblBuffs.Text = "Buffs (IconID): " + (buffs.Count > 0 ? string.Join(", ", buffs.Select(b => b.IconId)) : "Nenhum");
            lblStatus.Text = $"Estado: [{Bot.CurrentState}] - {Bot.StatusText}";
            if (_isPulsing) return;
            _isPulsing = true;
            Task.Run(() =>
            {
                try { Bot.Pulse(); }
                finally { _isPulsing = false; }
            });
        }
        private class LeaderOption
        {
            public uint Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => $"{Name} (ID: {Id})";
        }
    }
}