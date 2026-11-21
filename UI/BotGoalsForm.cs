using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Full_Bot
{
    public class BotGoalsForm : Form
    {
        private readonly BotController _bot;
        private readonly CharacterProfile _profile;
        private readonly Action _onSaved;

        private CheckBox chkAutoMobPull, chkAutoDefend, chkHasArea, chkUseFlight;
        private TextBox txtMobIds, txtMobPullRadius, txtAggroRadius;
        private TextBox txtCenterX, txtCenterY, txtCenterZ, txtAreaRadius;
        private TextBox txtFlyMinDist, txtFlyAlt;
        private Button btnCapturePos, btnSave, btnCancel;

        public BotGoalsForm(BotController bot, CharacterProfile profile, Action onSaved = null)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _onSaved = onSaved;

            Text = $"Metas da Rotina - {_bot.Player.Name}";
            Size = new Size(510, 485);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            BuildControls();
            LoadFromProfile();
        }

        private void BuildControls()
        {
            chkAutoMobPull = new CheckBox { Location = new Point(16, 16), Size = new Size(165, 24), Text = "Ativar Auto-Mobada" };
            var lblPullRad = new Label { Location = new Point(190, 19), Size = new Size(115, 20), Text = "Raio Mobada (m):" };
            txtMobPullRadius = new TextBox { Location = new Point(310, 16), Size = new Size(80, 22) };

            chkAutoDefend = new CheckBox { Location = new Point(16, 48), Size = new Size(165, 24), Text = "Defender de Agressores" };
            var lblAggroRad = new Label { Location = new Point(190, 51), Size = new Size(115, 20), Text = "Raio Aggro (m):" };
            txtAggroRadius = new TextBox { Location = new Point(310, 48), Size = new Size(80, 22) };

            var lblMobIds = new Label { Location = new Point(16, 84), Size = new Size(160, 20), Text = "Mobs Alvo (IDs Template):" };
            txtMobIds = new TextBox { Location = new Point(180, 81), Size = new Size(298, 22) };

            chkHasArea = new CheckBox { Location = new Point(16, 118), Size = new Size(215, 24), Text = "Definir Local Inicial / Perímetro" };
            btnCapturePos = new Button
            {
                Location = new Point(240, 116),
                Size = new Size(238, 26),
                Text = "Capturar Posição Atual do Char"
            };
            btnCapturePos.Click += (s, e) =>
            {
                chkHasArea.Checked = true;
                txtCenterX.Text = _bot.Player.X.ToString("F1", CultureInfo.InvariantCulture);
                txtCenterY.Text = _bot.Player.Y.ToString("F1", CultureInfo.InvariantCulture);
                txtCenterZ.Text = _bot.Player.Z.ToString("F1", CultureInfo.InvariantCulture);
            };

            var lblX = new Label { Location = new Point(16, 154), Size = new Size(45, 20), Text = "Raw X:" };
            txtCenterX = new TextBox { Location = new Point(62, 151), Size = new Size(95, 22) };

            var lblY = new Label { Location = new Point(168, 154), Size = new Size(45, 20), Text = "Raw Y:" };
            txtCenterY = new TextBox { Location = new Point(214, 151), Size = new Size(95, 22) };

            var lblZ = new Label { Location = new Point(320, 154), Size = new Size(45, 20), Text = "Raw Z:" };
            txtCenterZ = new TextBox { Location = new Point(366, 151), Size = new Size(112, 22) };

            var lblRadius = new Label { Location = new Point(16, 188), Size = new Size(155, 20), Text = "Perímetro / Raio (m):" };
            txtAreaRadius = new TextBox { Location = new Point(175, 185), Size = new Size(95, 22) };

            chkUseFlight = new CheckBox { Location = new Point(16, 222), Size = new Size(230, 24), Text = "Usar Voo se Longe (ToggleFly / MoveToFly)" };

            var lblFlyDist = new Label { Location = new Point(16, 256), Size = new Size(155, 20), Text = "Distância Mín. Voo (m):" };
            txtFlyMinDist = new TextBox { Location = new Point(175, 253), Size = new Size(95, 22) };

            var lblFlyAlt = new Label { Location = new Point(285, 256), Size = new Size(95, 20), Text = "Altitude Voo:" };
            txtFlyAlt = new TextBox { Location = new Point(384, 253), Size = new Size(94, 22) };

            var lblInfo = new Label
            {
                Location = new Point(16, 290),
                Size = new Size(462, 68),
                ForeColor = Color.DimGray,
                Text = "Comportamento: se sair do perímetro e a distância for >= Distância Mín. Voo, ativa o voo (MoveMode != 2 -> toggle), voa até o destino e, ao chegar, sai do voo (MoveMode == 2 -> toggle). A defesa prioriza agressores próximos."
            };

            btnSave = new Button
            {
                Location = new Point(172, 375),
                Size = new Size(150, 34),
                Text = "Salvar Metas",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnSave.Click += (s, e) => SaveToProfile();

            btnCancel = new Button
            {
                Location = new Point(330, 375),
                Size = new Size(150, 34),
                Text = "Cancelar"
            };
            btnCancel.Click += (s, e) => Close();

            Controls.AddRange(new Control[]
            {
                chkAutoMobPull, lblPullRad, txtMobPullRadius,
                chkAutoDefend, lblAggroRad, txtAggroRadius,
                lblMobIds, txtMobIds,
                chkHasArea, btnCapturePos,
                lblX, txtCenterX, lblY, txtCenterY, lblZ, txtCenterZ,
                lblRadius, txtAreaRadius,
                chkUseFlight, lblFlyDist, txtFlyMinDist, lblFlyAlt, txtFlyAlt,
                lblInfo, btnSave, btnCancel
            });
        }

        private void LoadFromProfile()
        {
            chkAutoMobPull.Checked = _profile.AutoMobPull;
            txtMobPullRadius.Text = _profile.MobPullRadius.ToString(CultureInfo.InvariantCulture);

            chkAutoDefend.Checked = _profile.AutoDefend;
            txtAggroRadius.Text = _profile.AggroDetectRadius.ToString(CultureInfo.InvariantCulture);

            txtMobIds.Text = string.Join(", ", _profile.FarmMobIds);

            chkHasArea.Checked = _profile.HasAreaCenter;
            txtCenterX.Text = _profile.AreaCenterX.ToString("F1", CultureInfo.InvariantCulture);
            txtCenterY.Text = _profile.AreaCenterY.ToString("F1", CultureInfo.InvariantCulture);
            txtCenterZ.Text = _profile.AreaCenterZ.ToString("F1", CultureInfo.InvariantCulture);
            txtAreaRadius.Text = _profile.AreaRadius.ToString(CultureInfo.InvariantCulture);

            chkUseFlight.Checked = _profile.UseFlightForLongDistance;
            txtFlyMinDist.Text = _profile.FlyMinDistance.ToString(CultureInfo.InvariantCulture);
            txtFlyAlt.Text = _profile.FlyAltitude.ToString(CultureInfo.InvariantCulture);
        }

        private void SaveToProfile()
        {
            _profile.AutoMobPull = chkAutoMobPull.Checked;
            _profile.MobPullRadius = ParseFloat(txtMobPullRadius.Text, 22f);

            _profile.AutoDefend = chkAutoDefend.Checked;
            _profile.AggroDetectRadius = ParseFloat(txtAggroRadius.Text, 16f);

            _profile.FarmMobIds = ParseUintList(txtMobIds.Text);

            _profile.HasAreaCenter = chkHasArea.Checked;
            _profile.AreaCenterX = ParseFloat(txtCenterX.Text, _bot.Player.X);
            _profile.AreaCenterY = ParseFloat(txtCenterY.Text, _bot.Player.Y);
            _profile.AreaCenterZ = ParseFloat(txtCenterZ.Text, _bot.Player.Z);
            _profile.AreaRadius = ParseFloat(txtAreaRadius.Text, 45f);

            _profile.UseFlightForLongDistance = chkUseFlight.Checked;
            _profile.FlyMinDistance = ParseFloat(txtFlyMinDist.Text, 60f);
            _profile.FlyAltitude = int.TryParse(txtFlyAlt.Text, out int alt) ? Math.Max(0, Math.Min(69, alt)) : 25;

            ProfileManager.SaveForChar(_bot.Player.Name, _profile);
            _onSaved?.Invoke();
            DialogResult = DialogResult.OK;
            Close();
        }

        private static float ParseFloat(string raw, float fallback)
        {
            if (float.TryParse((raw ?? string.Empty).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return v;
            return fallback;
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
    }
}