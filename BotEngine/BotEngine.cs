using System;

namespace Full_Bot
{
    public enum BotMode
    {
        AutoCultivoSolo = 0,
        AutoCultivoGrupo = 1,
        AutoFarme = 2,
        AutoColetaMatts = 3
    }

    public enum BotConfigWindow
    {
        BatalhaSolo = 0,
        BatalhaGrupo = 1,
        RotasMatts = 2,
        AutoCultivo = 3,
        Geral = 4
    }

    public partial class BotEngine
    {
        public PlayerSession Session { get; }
        public PlayerCore Player => Session?.Player;

        public bool IsEnabled { get; private set; }
        public BotMode Mode { get; private set; } = BotMode.AutoFarme;

        public event Action<bool> EnabledChanged;
        public event Action<BotMode> ModeChanged;
        public event Action<BotConfigWindow> ConfigWindowRequested;

        public BotEngine(PlayerSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public virtual void SetEnabled(bool enabled)
        {
            if (IsEnabled == enabled) return;
            IsEnabled = enabled;
            EnabledChanged?.Invoke(IsEnabled);
        }

        public virtual void SetMode(BotMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            ModeChanged?.Invoke(Mode);
        }

        public virtual void RequestConfigWindow(BotConfigWindow window)
        {
            ConfigWindowRequested?.Invoke(window);
        }
    }
}