using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Full_Bot
{
    public class BotSettingsForm : Form
    {
        private readonly BotController _bot;
        private readonly CharacterProfile _profile;
        private readonly Action _onSaved;

        private CheckBox chkAutoParty, chkAutoInviteFriends, chkHelpAllies;
        private ComboBox cmbRole, cmbLeader, cmbLootMode;
        private TextBox txtAllowedLeaders, txtInviteFriends, txtMaxLeaderDist;
        private TextBox txtLootRadius, txtCollectIds, txtBuffRules, txtAttackSkills;
        private Button btnSave, btnCancel;

        public BotSettingsForm(BotController bot, CharacterProfile profile, Action onSaved = null)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _onSaved = onSaved;

            Text = $"Configurações - {_bot.Player.Name}";
            Size = new Size(510, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            BuildControls();
            LoadFromProfile();
        }

        private void BuildControls()
        {
            chkAutoParty = new CheckBox { Location = new Point(16, 16), Size = new Size(140, 24), Text = "Auto-Aceitar PT" };
            chkAutoInviteFriends = new CheckBox { Location = new Point(165, 16), Size = new Size(160, 24), Text = "Auto-Chamar Amigos" };
            chkHelpAllies = new CheckBox { Location = new Point(330, 16), Size = new Size(155, 24), Text = "Ajudar Aliados" };

            var lblRole = new Label { Location = new Point(16, 52), Size = new Size(150, 20), Text = "Modo de Grupo:" };
            cmbRole = new ComboBox { Location = new Point(172, 49), Size = new Size(135, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRole.Items.AddRange(new object[] { "Solo", "Líder", "Seguidor" });

            cmbLeader = new ComboBox { Location = new Point(315, 49), Size = new Size(165, 24), DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            cmbRole.SelectedIndexChanged += (s, e) =>
            {
                cmbLeader.Enabled = (cmbRole.SelectedIndex == 2);
                RefreshLeaders(_profile.LeaderPlayerId);
            };

            var lblMaxDist = new Label { Location = new Point(16, 84), Size = new Size(150, 20), Text = "Raio Máx. Líder (m):" };
            txtMaxLeaderDist = new TextBox { Location = new Point(172, 81), Size = new Size(135, 22) };

            var lblAllowed = new Label { Location = new Point(16, 116), Size = new Size(150, 20), Text = "Aceitar PT só IDs:" };
            txtAllowedLeaders = new TextBox { Location = new Point(172, 113), Size = new Size(308, 22) };

            var lblFriends = new Label { Location = new Point(16, 148), Size = new Size(150, 20), Text = "IDs Amigos p/ Chamar:" };
            txtInviteFriends = new TextBox { Location = new Point(172, 145), Size = new Size(308, 22) };

            var lblLootMode = new Label { Location = new Point(16, 184), Size = new Size(150, 20), Text = "Modo de Loot:" };
            cmbLootMode = new ComboBox { Location = new Point(172, 181), Size = new Size(185, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLootMode.Items.AddRange(new object[] { "Nenhum", "Somente Moedas", "Lista Específica (IDs)", "Todos no Raio" });

            var lblLootRad = new Label { Location = new Point(365, 184), Size = new Size(45, 20), Text = "Raio:" };
            txtLootRadius = new TextBox { Location = new Point(414, 181), Size = new Size(66, 22) };

            var lblCollectIds = new Label { Location = new Point(16, 216), Size = new Size(150, 20), Text = "IDs Itens Permitidos:" };
            txtCollectIds = new TextBox { Location = new Point(172, 213), Size = new Size(308, 22) };

            var lblBuffs = new Label { Location = new Point(16, 252), Size = new Size(150, 20), Text = "Auto-Buff (IconId:SkillId):" };
            txtBuffRules = new TextBox { Location = new Point(172, 249), Size = new Size(308, 22) };

            var lblSkills = new Label { Location = new Point(16, 284), Size = new Size(150, 20), Text = "Skills Batalha (IDs):" };
            txtAttackSkills = new TextBox { Location = new Point(172, 281), Size = new Size(308, 22) };

            var lblHint = new Label
            {
                Location = new Point(16, 318),
                Size = new Size(464, 54),
                ForeColor = Color.DimGray,
                Text = "Dica: separe múltiplos IDs por vírgula (ex.: 1001, 1002). Para auto-buff, use IconId:SkillId separados por vírgula (ex.: 65:77, 120:145). 'Somente Moedas' coleta moedas automaticamente."
            };

            btnSave = new Button
            {
                Location = new Point(172, 395),
                Size = new Size(150, 34),
                Text = "Salvar e Aplicar",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnSave.Click += (s, e) => SaveToProfile();

            btnCancel = new Button
            {
                Location = new Point(330, 395),
                Size = new Size(150, 34),
                Text = "Cancelar"
            };
            btnCancel.Click += (s, e) => Close();

            Controls.AddRange(new Control[]
            {
                chkAutoParty, chkAutoInviteFriends, chkHelpAllies,
                lblRole, cmbRole, cmbLeader,
                lblMaxDist, txtMaxLeaderDist,
                lblAllowed, txtAllowedLeaders,
                lblFriends, txtInviteFriends,
                lblLootMode, cmbLootMode, lblLootRad, txtLootRadius,
                lblCollectIds, txtCollectIds,
                lblBuffs, txtBuffRules,
                lblSkills, txtAttackSkills,
                lblHint, btnSave, btnCancel
            });
        }

        private void LoadFromProfile()
        {
            chkAutoParty.Checked = _profile.AutoAcceptParty;
            chkAutoInviteFriends.Checked = _profile.AutoInviteFriends;
            chkHelpAllies.Checked = _profile.HelpAlliesInQuest;

            cmbRole.SelectedIndex = _profile.Role == "Lider" ? 1 : (_profile.Role == "Seguidor" ? 2 : 0);
            cmbLeader.Enabled = (cmbRole.SelectedIndex == 2);
            RefreshLeaders(_profile.LeaderPlayerId);

            txtMaxLeaderDist.Text = _profile.MaxLeaderDistance.ToString();
            txtAllowedLeaders.Text = string.Join(", ", _profile.AllowedPartyLeaderIds);
            txtInviteFriends.Text = string.Join(", ", _profile.AutoInviteFriendIds);

            cmbLootMode.SelectedIndex = (int)_profile.LootMode;
            txtLootRadius.Text = _profile.LootRadius.ToString();
            txtCollectIds.Text = string.Join(", ", _profile.CollectItemIds);

            txtBuffRules.Text = string.Join(", ", _profile.BuffRules.Select(b => $"{b.IconId}:{b.SkillId}"));
            txtAttackSkills.Text = string.Join(", ", _profile.AttackSkills);
        }

        private void RefreshLeaders(uint preferredLeaderId)
        {
            cmbLeader.Items.Clear();
            int selectedIdx = -1;

            foreach (var b in BotHub.GetSnapshot())
            {
                if (b?.Player != null && b.Player.Id != 0 && b.Player.Id != _bot.Player.Id)
                {
                    var opt = new LeaderItem { Id = b.Player.Id, Name = b.Player.Name };
                    int idx = cmbLeader.Items.Add(opt);
                    if (preferredLeaderId != 0 && b.Player.Id == preferredLeaderId)
                        selectedIdx = idx;
                }
            }

            if (selectedIdx >= 0)
                cmbLeader.SelectedIndex = selectedIdx;
            else if (cmbLeader.Items.Count > 0)
                cmbLeader.SelectedIndex = 0;
        }

        private void SaveToProfile()
        {
            _profile.AutoAcceptParty = chkAutoParty.Checked;
            _profile.AutoInviteFriends = chkAutoInviteFriends.Checked;
            _profile.HelpAlliesInQuest = chkHelpAllies.Checked;

            _profile.Role = cmbRole.SelectedIndex == 1 ? "Lider" : (cmbRole.SelectedIndex == 2 ? "Seguidor" : "Solo");
            _profile.LeaderPlayerId = (cmbRole.SelectedIndex == 2 && cmbLeader.SelectedItem is LeaderItem opt) ? opt.Id : 0;
            _profile.MaxLeaderDistance = float.TryParse(txtMaxLeaderDist.Text, out float maxD) && maxD > 0 ? maxD : 100f;

            _profile.AllowedPartyLeaderIds = ParseUintList(txtAllowedLeaders.Text);
            _profile.AutoInviteFriendIds = ParseUintList(txtInviteFriends.Text);

            _profile.LootMode = (LootFilterMode)cmbLootMode.SelectedIndex;
            _profile.LootRadius = float.TryParse(txtLootRadius.Text, out float rad) && rad > 0 ? rad : 6f;
            _profile.CollectItemIds = ParseUintList(txtCollectIds.Text);

            _profile.AttackSkills = ParseUintList(txtAttackSkills.Text);

            _profile.BuffRules.Clear();
            foreach (var pair in txtBuffRules.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Trim().Split(':');
                if (parts.Length == 2 && ushort.TryParse(parts[0].Trim(), out ushort icon) && uint.TryParse(parts[1].Trim(), out uint skill))
                {
                    _profile.BuffRules.Add(new AutoBuffRule
                    {
                        IconId = icon,
                        SkillId = skill,
                        BuffPartyAllies = true
                    });
                }
            }

            ProfileManager.SaveForChar(_bot.Player.Name, _profile);
            _onSaved?.Invoke();
            DialogResult = DialogResult.OK;
            Close();
        }

        private static List<uint> ParseUintList(string raw)
        {
            var list = new List<uint>();
            if (string.IsNullOrWhiteSpace(raw)) return list;

            foreach (var token in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (uint.TryParse(token.Trim(), out uint val))
                    list.Add(val);
            }
            return list;
        }

        private class LeaderItem
        {
            public uint Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => $"{Name} (ID: {Id})";
        }
    }
}