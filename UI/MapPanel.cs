using System.Drawing;
using System.Windows.Forms;

namespace Full_Bot
{
    public class MapPanel : Panel
    {
        private const float WorldScale = 0.35f;

        private PlayerCore _player;
        private WorldEntities _world;

        public MapPanel()
        {
            BackColor = Color.FromArgb(18, 20, 24);
            DoubleBuffered = true;
        }

        public void Bind(PlayerCore player, WorldEntities world)
        {
            _player = player;
            _world = world;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_player == null || _world == null) return;

            var g = e.Graphics;
            float centerX = Width / 2f;
            float centerY = Height / 2f;

            float pDispX = PlayerCore.ToDisplayX(_player.X);
            float pDispZ = PlayerCore.ToDisplayZ(_player.Z);

            var mobs = _world.GetMobsAndNpcs();
            foreach (var m in mobs)
            {
                if (m.HP == 0 && m.Type == 6) continue;

                float dx = (PlayerCore.ToDisplayX(m.X) - pDispX) * WorldScale + centerX;
                float dy = (PlayerCore.ToDisplayZ(m.Z) - pDispZ) * WorldScale + centerY;

                if (dx < -10 || dx > Width + 10 || dy < -10 || dy > Height + 10)
                    continue;

                Brush brush = m.Type == 7 ? Brushes.Gold : Brushes.IndianRed;
                g.FillEllipse(brush, dx - 3f, dy - 3f, 6f, 6f);
            }

            // Jogador no centro
            g.FillEllipse(Brushes.LimeGreen, centerX - 5f, centerY - 5f, 10f, 10f);
            g.DrawEllipse(Pens.White, centerX - 5f, centerY - 5f, 10f, 10f);
        }
    }
}