using System.Drawing;
using System.Windows.Forms;

namespace Full_Bot
{
    public class PlayerStatsPanel : Panel
    {
        private readonly Label _lblStatus;
        private readonly Label _lblInfo;

        public PlayerStatsPanel()
        {
            BackColor = Color.FromArgb(28, 30, 36);
            ForeColor = Color.Gainsboro;
            Padding = new Padding(10);

            _lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                ForeColor = Color.LightSkyBlue,
                Text = "Inicializando..."
            };

            _lblInfo = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9f),
                ForeColor = Color.Gainsboro,
                Text = string.Empty
            };

            Controls.Add(_lblInfo);
            Controls.Add(_lblStatus);
        }

        public void SetStatus(string status)
        {
            _lblStatus.Text = status;
        }

        public void UpdateData(PlayerCore p, int buffCount, int bagCount, int partyCount, int questCount, int entityCount)
        {
            float dispX = PlayerCore.ToDisplayX(p.X);
            float dispY = PlayerCore.ToDisplayY(p.Y);
            float dispZ = PlayerCore.ToDisplayZ(p.Z);

            _lblInfo.Text =
                $"Nome: {p.Name} (ID: {p.Id})\n" +
                $"Lvl: {p.Level} | HP: {p.HP}/{p.HPMax} | MP: {p.MP}/{p.MPMax}\n" +
                $"Def. Física: {p.DefFisica}\n" +
                $"Raw:   ({p.X:F1}, {p.Y:F1}, {p.Z:F1})\n" +
                $"Tela:  ({dispX:F1}, {dispY:F1}, {dispZ:F1})\n" +
                $"Região: {p.CurrentRegionName}\n" +
                $"Target ID: 0x{p.TargetId:X}\n" +
                $"Buffs: {buffCount} | Bag: {bagCount} | Party: {partyCount}\n" +
                $"Quests: {questCount} | Entidades: {entityCount}";
        }
    }
}