using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Full_Bot
{
    public class WhoAmISettingsForm : Form
    {
        private readonly string _charName;
        private readonly WhoAmIConfig _config;
        private readonly Action _onSaved;

        private CheckBox chkGroupRoleEnabled;
        private ComboBox cmbRole;
        private CheckBox chkAutoBoss, chkKeepDist, chkTankTaunt, chkWaitAssistant, chkRescueSupport;
        private CheckBox chkSingleHeal, chkGroupHeal, chkRevive;
        private TextBox txtBossHp, txtMinSafeDist, txtPreferredDist, txtTauntSkill, txtHealSkill, txtGroupHealSkill, txtReviveSkill;
        private Button btnSave, btnClose;

        public WhoAmISettingsForm(string charName, WhoAmIConfig config, Action onSaved = null)
        {
            _charName = charName;
            _config = config ?? new WhoAmIConfig();
            if (_config.Distance == null) _config.Distance = new DistanceAndKitingConfig();
            if (_config.Skills == null) _config.Skills = new SpecializedSkillsConfig();
            _onSaved = onSaved;

            Text = $"Papel em Grupo (WhoAmI) - {_charName}";
            Size = new Size(570, 505);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            BuildUi();
            LoadValues();
        }

        private void BuildUi()
        {
            chkGroupRoleEnabled = new CheckBox { Location = new Point(14, 16), Size = new Size(135, 22), Text = "Ativo em Grupo" };

            var lblRole = new Label { Location = new Point(155, 18), Size = new Size(55, 20), Text = "Papel:" };
            cmbRole = new ComboBox { Location = new Point(212, 15), Size = new Size(150, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRole.Items.AddRange(new object[] { "Tank (Tanque)", "Assistant (Assistente)", "Damage (Dano)", "Support (Suporte)" });

            chkAutoBoss = new CheckBox { Location = new Point(372, 16), Size = new Size(175, 22), Text = "Detectar Boss Auto" };

            var lblBossHp = new Label { Location = new Point(14, 52), Size = new Size(115, 20), Text = "HP Mín. Boss:" };
            txtBossHp = new TextBox { Location = new Point(132, 49), Size = new Size(100, 22) };

            chkKeepDist = new CheckBox { Location = new Point(250, 50), Size = new Size(165, 22), Text = "Manter Distância Mobs" };
            var lblSafe = new Label { Location = new Point(422, 52), Size = new Size(62, 20), Text = "Mín (m):" };
            txtMinSafeDist = new TextBox { Location = new Point(486, 49), Size = new Size(56, 22) };

            var lblPref = new Label { Location = new Point(14, 84), Size = new Size(145, 20), Text = "Distância Ideal (m):" };
            txtPreferredDist = new TextBox { Location = new Point(162, 81), Size = new Size(60, 22) };

            chkWaitAssistant = new CheckBox { Location = new Point(240, 82), Size = new Size(305, 22), Text = "Dano Espera Assistente Após Tank (DG/Boss)" };

            chkRescueSupport = new CheckBox { Location = new Point(14, 116), Size = new Size(260, 22), Text = "Dano Socorre Suporte em Perigo" };
            chkTankTaunt = new CheckBox { Location = new Point(285, 116), Size = new Size(135, 22), Text = "Tank Taunt Ativo" };
            txtTauntSkill = new TextBox { Location = new Point(424, 114), Size = new Size(118, 22) };

            chkSingleHeal = new CheckBox { Location = new Point(14, 152), Size = new Size(145, 22), Text = "Cura Individual" };
            txtHealSkill = new TextBox { Location = new Point(162, 150), Size = new Size(95, 22) };

            chkGroupHeal = new CheckBox { Location = new Point(275, 152), Size = new Size(140, 22), Text = "Cura em Grupo" };
            txtGroupHealSkill = new TextBox { Location = new Point(420, 150), Size = new Size(122, 22) };

            chkRevive = new CheckBox { Location = new Point(14, 186), Size = new Size(145, 22), Text = "Reviver Membro" };
            txtReviveSkill = new TextBox { Location = new Point(162, 184), Size = new Size(95, 22) };

            var lblNote = new Label
            {
                Location = new Point(14, 224),
                Size = new Size(528, 145),
                Text = "Comportamento por papel na configuração do char:\n" +
                       "• Ao entrar em PT, cada char já usa seu papel configurado (Tank, Assistente, Dano ou Suporte).\n" +
                       "• DG/Boss: Tank na frente -> Assistente bate após Tank -> Dano bate após Assistente -> Suporte na retaguarda, foge e pede socorro se ameaçado, foca cura no Tank (ou geral) e revive.\n" +
                       "• Mundo aberto: fora de grupo usa rotina solo; em grupo todos atacam e Suporte cura.\n" +
                       "• Cancelamento de AoE e conjurações de bosses: já previsto no config (aguardando offsets)."
            };

            btnSave = new Button
            {
                Location = new Point(14, 405),
                Size = new Size(260, 36),
                Text = "Salvar Papel do Char (JSON)",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnSave.Click += (s, e) => SaveConfig();

            btnClose = new Button { Location = new Point(284, 405), Size = new Size(258, 36), Text = "Fechar" };
            btnClose.Click += (s, e) => Close();

            Controls.AddRange(new Control[]
            {
                chkGroupRoleEnabled, lblRole, cmbRole, chkAutoBoss,
                lblBossHp, txtBossHp, chkKeepDist, lblSafe, txtMinSafeDist,
                lblPref, txtPreferredDist, chkWaitAssistant,
                chkRescueSupport, chkTankTaunt, txtTauntSkill,
                chkSingleHeal, txtHealSkill, chkGroupHeal, txtGroupHealSkill,
                chkRevive, txtReviveSkill,
                lblNote, btnSave, btnClose
            });
        }

        private void LoadValues()
        {
            chkGroupRoleEnabled.Checked = _config.GroupRoleEnabled;
            cmbRole.SelectedIndex = (int)_config.GroupRole;
            chkAutoBoss.Checked = _config.AutoDetectBossByNameOrHp;
            txtBossHp.Text = _config.BossHpThresholdToTreatAsBoss.ToString(CultureInfo.InvariantCulture);

            chkKeepDist.Checked = _config.Distance.KeepDistanceFromMobs;
            txtMinSafeDist.Text = _config.Distance.MinSafeMobDistance.ToString(CultureInfo.InvariantCulture);
            txtPreferredDist.Text = _config.Distance.PreferredCombatDistance.ToString(CultureInfo.InvariantCulture);

            chkWaitAssistant.Checked = _config.Skills.DamageWaitForAssistantOrTank;
            chkRescueSupport.Checked = _config.Skills.DamageRescueSupportWhenThreatened;
            chkTankTaunt.Checked = _config.Skills.EnableTankTaunt;
            txtTauntSkill.Text = _config.Skills.TankTauntSkillId.ToString(CultureInfo.InvariantCulture);

            chkSingleHeal.Checked = _config.Skills.EnableSingleHeal;
            txtHealSkill.Text = _config.Skills.SingleHealSkillId.ToString(CultureInfo.InvariantCulture);

            chkGroupHeal.Checked = _config.Skills.EnableGroupHeal;
            txtGroupHealSkill.Text = _config.Skills.GroupHealSkillId.ToString(CultureInfo.InvariantCulture);

            chkRevive.Checked = _config.Skills.EnableRevive;
            txtReviveSkill.Text = _config.Skills.ReviveSkillId.ToString(CultureInfo.InvariantCulture);
        }

        private void SaveConfig()
        {
            _config.GroupRoleEnabled = chkGroupRoleEnabled.Checked;
            _config.GroupRole = (WhoAmIRole)Math.Max(0, Math.Min(3, cmbRole.SelectedIndex));
            _config.AutoDetectBossByNameOrHp = chkAutoBoss.Checked;
            _config.BossHpThresholdToTreatAsBoss = ParseFloat(txtBossHp.Text, 500000f);

            _config.Distance.KeepDistanceFromMobs = chkKeepDist.Checked;
            _config.Distance.MinSafeMobDistance = ParseFloat(txtMinSafeDist.Text, 8.0f);
            _config.Distance.PreferredCombatDistance = ParseFloat(txtPreferredDist.Text, 12.0f);

            _config.Skills.DamageWaitForAssistantOrTank = chkWaitAssistant.Checked;
            _config.Skills.DamageRescueSupportWhenThreatened = chkRescueSupport.Checked;
            _config.Skills.EnableTankTaunt = chkTankTaunt.Checked;
            uint.TryParse(txtTauntSkill.Text.Trim(), out uint tauntId);
            _config.Skills.TankTauntSkillId = tauntId;

            _config.Skills.EnableSingleHeal = chkSingleHeal.Checked;
            uint.TryParse(txtHealSkill.Text.Trim(), out uint healId);
            _config.Skills.SingleHealSkillId = healId;

            _config.Skills.EnableGroupHeal = chkGroupHeal.Checked;
            uint.TryParse(txtGroupHealSkill.Text.Trim(), out uint gHealId);
            _config.Skills.GroupHealSkillId = gHealId;

            _config.Skills.EnableRevive = chkRevive.Checked;
            uint.TryParse(txtReviveSkill.Text.Trim(), out uint revId);
            _config.Skills.ReviveSkillId = revId;

            _config.SaveForChar(_charName);
            _onSaved?.Invoke();
            DialogResult = DialogResult.OK;
            Close();
        }

        private static float ParseFloat(string raw, float fallback)
        {
            if (float.TryParse((raw ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return v;
            return fallback;
        }
    }
}