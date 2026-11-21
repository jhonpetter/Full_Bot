using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Full_Bot
{
    public class SelfCultivationSettingsForm : Form
    {
        private readonly string _charName;
        private readonly SelfCultivationConfig _config;
        private readonly Action _onSaved;

        private CheckBox chkEnabled, chkAutoPick, chkWaitParty, chkHoverHigh, chkSoloAvoidBoss, chkHighLevelHelp, chkShareInfo;
        private TextBox txtMinGroupLvl, txtMaxLvlDiff, txtFullPartySize, txtHoverAlt, txtCarryDiff;
        private Button btnSave, btnClose;

        public SelfCultivationSettingsForm(string charName, SelfCultivationConfig config, Action onSaved = null)
        {
            _charName = charName;
            _config = config ?? SelfCultivationConfig.CreateDefault();
            _onSaved = onSaved;

            Text = $"Auto Cultivo (SelfCultivation) - {_charName}";
            Size = new Size(540, 440);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            BuildUi();
            LoadValues();
        }

        private void BuildUi()
        {
            chkEnabled = new CheckBox { Location = new Point(14, 14), Size = new Size(160, 22), Text = "Auto Cultivo Ativo" };
            chkAutoPick = new CheckBox { Location = new Point(185, 14), Size = new Size(210, 22), Text = "Pegar Quest no Jornal Auto" };
            chkSoloAvoidBoss = new CheckBox { Location = new Point(400, 14), Size = new Size(125, 22), Text = "Solo Evita Boss" };

            chkWaitParty = new CheckBox { Location = new Point(14, 44), Size = new Size(210, 22), Text = "Aguardar PT no Local da Quest" };
            chkHoverHigh = new CheckBox { Location = new Point(230, 44), Size = new Size(175, 22), Text = "Voar Alto ao Aguardar" };
            var lblAlt = new Label { Location = new Point(410, 46), Size = new Size(62, 20), Text = "Alt. (m):" };
            txtHoverAlt = new TextBox { Location = new Point(474, 43), Size = new Size(48, 22) };

            var lblMinLvl = new Label { Location = new Point(14, 78), Size = new Size(155, 20), Text = "Lvl Mín. PT Cultivo:" };
            txtMinGroupLvl = new TextBox { Location = new Point(172, 75), Size = new Size(52, 22) };

            var lblDiff = new Label { Location = new Point(240, 78), Size = new Size(145, 20), Text = "Dif. Lvl PT Próximo:" };
            txtMaxLvlDiff = new TextBox { Location = new Point(388, 75), Size = new Size(48, 22) };

            var lblPtFull = new Label { Location = new Point(445, 78), Size = new Size(52, 20), Text = "PT Full:" };
            txtFullPartySize = new TextBox { Location = new Point(498, 75), Size = new Size(24, 22) };

            chkHighLevelHelp = new CheckBox { Location = new Point(14, 112), Size = new Size(255, 22), Text = "Lvl Muito Acima Ajuda Outros na PT" };
            var lblCarry = new Label { Location = new Point(275, 114), Size = new Size(85, 20), Text = "Dif. Lvl Alto:" };
            txtCarryDiff = new TextBox { Location = new Point(362, 111), Size = new Size(48, 22) };
            chkShareInfo = new CheckBox { Location = new Point(418, 112), Size = new Size(110, 22), Text = "Compartilhar" };

            var lblInfo = new Label
            {
                Location = new Point(14, 150),
                Size = new Size(508, 175),
                Text = "Regras aplicadas:\n" +
                       "• Em grupo: inicia acima do nível configurado (padrão 18) ou quando estiverem no mesmo range de nível.\n" +
                       "• Local da quest: pega no jornal se disponível e aguarda membros voando alto se marcado.\n" +
                       "• Bosses: 1 mob qtd 1 = Boss (PT completa); até 5 tipos qtd 1 cada = múltiplos bosses; solo evita bosses.\n" +
                       "• Nível muito acima na PT: ignora próprio cultivo e ajuda os demais compartilhando local e mobs.\n" +
                       "• Expansão futura: estrutura pronta para leitura automática de quest-log/NPC e integração com Move/Combat."
            };

            btnSave = new Button
            {
                Location = new Point(14, 350),
                Size = new Size(248, 36),
                Text = "Salvar Cultivo (JSON)",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnSave.Click += (s, e) => SaveConfig();

            btnClose = new Button { Location = new Point(274, 350), Size = new Size(248, 36), Text = "Fechar" };
            btnClose.Click += (s, e) => Close();

            Controls.AddRange(new Control[]
            {
                chkEnabled, chkAutoPick, chkSoloAvoidBoss,
                chkWaitParty, chkHoverHigh, lblAlt, txtHoverAlt,
                lblMinLvl, txtMinGroupLvl, lblDiff, txtMaxLvlDiff, lblPtFull, txtFullPartySize,
                chkHighLevelHelp, lblCarry, txtCarryDiff, chkShareInfo,
                lblInfo, btnSave, btnClose
            });
        }

        private void LoadValues()
        {
            chkEnabled.Checked = _config.Enabled;
            chkAutoPick.Checked = _config.AutoPickQuestFromJournal;
            chkSoloAvoidBoss.Checked = _config.SoloAvoidBosses;

            chkWaitParty.Checked = _config.WaitForPartyAtQuestLocation;
            chkHoverHigh.Checked = _config.PreferHoverHighWhileWaiting;
            txtHoverAlt.Text = _config.FlyHoverAltitude.ToString(CultureInfo.InvariantCulture);

            txtMinGroupLvl.Text = _config.MinGroupCultivationLevel.ToString(CultureInfo.InvariantCulture);
            txtMaxLvlDiff.Text = _config.MaxLevelDiffForGroup.ToString(CultureInfo.InvariantCulture);
            txtFullPartySize.Text = _config.FullPartySize.ToString(CultureInfo.InvariantCulture);

            chkHighLevelHelp.Checked = _config.HighLevelHelpOthersEnabled;
            txtCarryDiff.Text = _config.HighLevelCarryDiffThreshold.ToString(CultureInfo.InvariantCulture);
            chkShareInfo.Checked = _config.ShareLocationAndMobsWithParty;
        }

        private void SaveConfig()
        {
            _config.Enabled = chkEnabled.Checked;
            _config.AutoPickQuestFromJournal = chkAutoPick.Checked;
            _config.SoloAvoidBosses = chkSoloAvoidBoss.Checked;

            _config.WaitForPartyAtQuestLocation = chkWaitParty.Checked;
            _config.PreferHoverHighWhileWaiting = chkHoverHigh.Checked;
            _config.FlyHoverAltitude = ParseFloat(txtHoverAlt.Text, 18.0f);

            int.TryParse(txtMinGroupLvl.Text.Trim(), out int minLvl);
            int.TryParse(txtMaxLvlDiff.Text.Trim(), out int maxDiff);
            int.TryParse(txtFullPartySize.Text.Trim(), out int fullPt);
            int.TryParse(txtCarryDiff.Text.Trim(), out int carryDiff);

            _config.MinGroupCultivationLevel = minLvl > 0 ? minLvl : 18;
            _config.MaxLevelDiffForGroup = maxDiff >= 0 ? maxDiff : 3;
            _config.FullPartySize = fullPt >= 2 ? fullPt : 6;
            _config.HighLevelCarryDiffThreshold = carryDiff > 0 ? carryDiff : 15;
            _config.ShareLocationAndMobsWithParty = chkShareInfo.Checked;

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