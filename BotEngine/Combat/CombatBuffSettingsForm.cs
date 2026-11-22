using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Full_Bot
{
    public class CombatBuffSettingsForm : Form
    {
        private readonly string _charName;
        private readonly CombatBuffConfig _config;
        private readonly Action _onSaved;

        private CheckBox chkSoloEnabled, chkOnlyNotKilling, chkGroupEnabled, chkGatherLeader;
        private ComboBox cmbCasterKind, cmbArchetype;
        private TextBox txtSoloSafeDist, txtGatherMinDist, txtSyncWait, txtCooldown;
        private ListBox lstBuffs;
        private TextBox txtBuffName, txtSkillId, txtIconId;
        private ComboBox cmbTargetType;
        private CheckBox chkIncludeSelf, chkBuffActive;
        private Button btnAddOrUpdate, btnRemove, btnSave, btnClose;

        public CombatBuffSettingsForm(string charName, CombatBuffConfig config, Action onSaved = null)
        {
            _charName = charName;
            _config = config ?? new CombatBuffConfig();
            if (_config.Buffs == null)
                _config.Buffs = new List<BuffEntryConfig>();
            _onSaved = onSaved;

            Text = $"Configurações de Buffs e Classe - {_charName}";
            Size = new Size(545, 550);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            BuildUi();
            LoadValues();
        }

        private void BuildUi()
        {
            var lblCaster = new Label { Location = new Point(14, 15), Size = new Size(115, 20), Text = "Tipo de Ataque:" };
            cmbCasterKind = new ComboBox { Location = new Point(132, 12), Size = new Size(195, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCasterKind.Items.AddRange(new object[]
            {
                "Físico / Arqueiro (AutoAttack)",
                "Mágico - Mago/Sacerdote (Só Skills)"
            });

            var lblArch = new Label { Location = new Point(335, 15), Size = new Size(62, 20), Text = "Alcance:" };
            cmbArchetype = new ComboBox { Location = new Point(399, 12), Size = new Size(125, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbArchetype.Items.AddRange(new object[]
            {
                "Físico (Perto)",
                "Distância (Mag/Arq)"
            });

            chkSoloEnabled = new CheckBox { Location = new Point(14, 44), Size = new Size(140, 22), Text = "Buff Solo Ativo" };
            chkOnlyNotKilling = new CheckBox { Location = new Point(160, 44), Size = new Size(210, 22), Text = "Só Buffar Fora de Batalha" };
            var lblSafe = new Label { Location = new Point(375, 46), Size = new Size(85, 20), Text = "Dist. Mob (m):" };
            txtSoloSafeDist = new TextBox { Location = new Point(462, 43), Size = new Size(62, 22) };

            chkGroupEnabled = new CheckBox { Location = new Point(14, 72), Size = new Size(140, 22), Text = "Buff Grupo Ativo" };
            chkGatherLeader = new CheckBox { Location = new Point(160, 72), Size = new Size(175, 22), Text = "Juntar Perto do Líder" };
            var lblMinLeader = new Label { Location = new Point(340, 74), Size = new Size(118, 20), Text = "Dist. Mín. Líder (m):" };
            txtGatherMinDist = new TextBox { Location = new Point(462, 71), Size = new Size(62, 22) };

            var lblSync = new Label { Location = new Point(14, 102), Size = new Size(135, 20), Text = "Aguardar Grupo (s):" };
            txtSyncWait = new TextBox { Location = new Point(150, 99), Size = new Size(65, 22) };

            var lblCd = new Label { Location = new Point(235, 102), Size = new Size(125, 20), Text = "Cooldown Cast (s):" };
            txtCooldown = new TextBox { Location = new Point(362, 99), Size = new Size(65, 22) };

            lstBuffs = new ListBox { Location = new Point(14, 132), Size = new Size(510, 125) };
            lstBuffs.SelectedIndexChanged += (s, e) => PopulateEditorFromSelected();

            var lblName = new Label { Location = new Point(14, 268), Size = new Size(55, 20), Text = "Nome:" };
            txtBuffName = new TextBox { Location = new Point(72, 265), Size = new Size(145, 22) };

            var lblSkill = new Label { Location = new Point(226, 268), Size = new Size(55, 20), Text = "Skill ID:" };
            txtSkillId = new TextBox { Location = new Point(284, 265), Size = new Size(85, 22) };

            var lblIcon = new Label { Location = new Point(378, 268), Size = new Size(55, 20), Text = "Icon ID:" };
            txtIconId = new TextBox { Location = new Point(436, 265), Size = new Size(88, 22) };

            var lblType = new Label { Location = new Point(14, 300), Size = new Size(85, 20), Text = "Aplicação:" };
            cmbTargetType = new ComboBox { Location = new Point(102, 297), Size = new Size(210, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTargetType.Items.AddRange(new object[] { "Sem Target (Self / AoE)", "Por Target (Inclui Self)" });

            chkIncludeSelf = new CheckBox { Location = new Point(320, 298), Size = new Size(105, 22), Text = "Incluir Self" };
            chkBuffActive = new CheckBox { Location = new Point(430, 298), Size = new Size(95, 22), Text = "Ativo" };

            btnAddOrUpdate = new Button { Location = new Point(14, 334), Size = new Size(250, 30), Text = "Adicionar / Atualizar Buff" };
            btnAddOrUpdate.Click += (s, e) => AddOrUpdateBuff();

            btnRemove = new Button { Location = new Point(274, 334), Size = new Size(250, 30), Text = "Remover Selecionado" };
            btnRemove.Click += (s, e) => RemoveSelectedBuff();

            btnSave = new Button
            {
                Location = new Point(14, 455),
                Size = new Size(250, 36),
                Text = "Salvar Buffs e Classe (JSON)",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnSave.Click += (s, e) => SaveConfig();

            btnClose = new Button { Location = new Point(274, 455), Size = new Size(250, 36), Text = "Fechar" };
            btnClose.Click += (s, e) => Close();

            Controls.AddRange(new Control[]
            {
                lblCaster, cmbCasterKind, lblArch, cmbArchetype,
                chkSoloEnabled, chkOnlyNotKilling, lblSafe, txtSoloSafeDist,
                chkGroupEnabled, chkGatherLeader, lblMinLeader, txtGatherMinDist,
                lblSync, txtSyncWait, lblCd, txtCooldown,
                lstBuffs,
                lblName, txtBuffName, lblSkill, txtSkillId, lblIcon, txtIconId,
                lblType, cmbTargetType, chkIncludeSelf, chkBuffActive,
                btnAddOrUpdate, btnRemove, btnSave, btnClose
            });
        }

        private void LoadValues()
        {
            cmbCasterKind.SelectedIndex = _config.CasterKind == CombatCasterKind.MagicCaster ? 1 : 0;
            cmbArchetype.SelectedIndex = _config.Archetype == CombatArchetype.MagicoArqueiro ? 1 : 0;

            chkSoloEnabled.Checked = _config.SoloBuffEnabled;
            chkOnlyNotKilling.Checked = _config.OnlyBuffWhenNotKillingMob;
            txtSoloSafeDist.Text = _config.SoloSafeDistanceFromMobs.ToString(CultureInfo.InvariantCulture);

            chkGroupEnabled.Checked = _config.GroupBuffEnabled;
            chkGatherLeader.Checked = _config.GroupGatherNearLeader;
            txtGatherMinDist.Text = _config.GroupGatherMinDistanceToLeader.ToString(CultureInfo.InvariantCulture);

            txtSyncWait.Text = _config.GroupSyncWaitSeconds.ToString(CultureInfo.InvariantCulture);
            txtCooldown.Text = _config.BuffCastCooldownSeconds.ToString(CultureInfo.InvariantCulture);

            cmbTargetType.SelectedIndex = 0;
            chkIncludeSelf.Checked = true;
            chkBuffActive.Checked = true;

            RefreshBuffList();
        }

        private void RefreshBuffList()
        {
            lstBuffs.Items.Clear();
            foreach (var b in _config.Buffs)
            {
                string mode = b.TargetType == BuffTargetType.SingleTargetIncludeSelf ? "Target+Self" : "NoTarget";
                string state = b.Enabled ? "ON" : "OFF";
                lstBuffs.Items.Add($"[{state}] {b.Name} | Skill={b.SkillId} | Icon={b.IconId} | {mode}");
            }
        }

        private void PopulateEditorFromSelected()
        {
            int idx = lstBuffs.SelectedIndex;
            if (idx < 0 || idx >= _config.Buffs.Count) return;

            var b = _config.Buffs[idx];
            txtBuffName.Text = b.Name;
            txtSkillId.Text = b.SkillId.ToString(CultureInfo.InvariantCulture);
            txtIconId.Text = b.IconId.ToString(CultureInfo.InvariantCulture);
            cmbTargetType.SelectedIndex = b.TargetType == BuffTargetType.SingleTargetIncludeSelf ? 1 : 0;
            chkIncludeSelf.Checked = b.IncludeSelf;
            chkBuffActive.Checked = b.Enabled;
        }

        private void AddOrUpdateBuff()
        {
            if (!uint.TryParse(txtSkillId.Text.Trim(), out uint skillId) || skillId == 0)
            {
                MessageBox.Show("Informe um Skill ID válido.", "Buffs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ushort.TryParse(txtIconId.Text.Trim(), out ushort iconId);
            string name = string.IsNullOrWhiteSpace(txtBuffName.Text) ? $"Buff_{skillId}" : txtBuffName.Text.Trim();
            var tType = cmbTargetType.SelectedIndex == 1 ? BuffTargetType.SingleTargetIncludeSelf : BuffTargetType.SelfOrAoENoTarget;

            var entry = new BuffEntryConfig
            {
                Name = name,
                SkillId = skillId,
                IconId = iconId,
                TargetType = tType,
                IncludeSelf = chkIncludeSelf.Checked,
                Enabled = chkBuffActive.Checked
            };

            int idx = lstBuffs.SelectedIndex;
            if (idx >= 0 && idx < _config.Buffs.Count)
                _config.Buffs[idx] = entry;
            else
                _config.Buffs.Add(entry);

            RefreshBuffList();
        }

        private void RemoveSelectedBuff()
        {
            int idx = lstBuffs.SelectedIndex;
            if (idx < 0 || idx >= _config.Buffs.Count) return;
            _config.Buffs.RemoveAt(idx);
            RefreshBuffList();
        }

        private void SaveConfig()
        {
            _config.CasterKind = cmbCasterKind.SelectedIndex == 1
                ? CombatCasterKind.MagicCaster
                : CombatCasterKind.PhysicalOrArcher;

            _config.Archetype = cmbArchetype.SelectedIndex == 1
                ? CombatArchetype.MagicoArqueiro
                : CombatArchetype.Fisico;

            _config.SoloBuffEnabled = chkSoloEnabled.Checked;
            _config.OnlyBuffWhenNotKillingMob = chkOnlyNotKilling.Checked;
            _config.SoloSafeDistanceFromMobs = ParseFloat(txtSoloSafeDist.Text, 18f);

            _config.GroupBuffEnabled = chkGroupEnabled.Checked;
            _config.GroupGatherNearLeader = chkGatherLeader.Checked;
            _config.GroupGatherMinDistanceToLeader = ParseFloat(txtGatherMinDist.Text, 6f);

            _config.GroupSyncWaitSeconds = ParseDouble(txtSyncWait.Text, 8.0);
            _config.BuffCastCooldownSeconds = ParseDouble(txtCooldown.Text, 0.85);

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

        private static double ParseDouble(string raw, double fallback)
        {
            if (double.TryParse((raw ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                return v;
            return fallback;
        }
    }
}