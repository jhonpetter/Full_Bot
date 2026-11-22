using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Full_Bot
{
    internal static class DailyBotLogger
    {
        private static readonly object _logLock = new object();

        public static string GetTodayLogPath()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string fileName = $"BotLog_{DateTime.Now:yyyy-MM-dd}.txt";
            return Path.Combine(folder, fileName);
        }

        public static void Append(string charName, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            lock (_logLock)
            {
                string path = GetTodayLogPath();
                string safeChar = string.IsNullOrWhiteSpace(charName) ? "Bot" : charName;
                string line = $"[{DateTime.Now:HH:mm:ss}] [{safeChar}] {message}{Environment.NewLine}";
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }

        public static string ReadTodayLog()
        {
            lock (_logLock)
            {
                string path = GetTodayLogPath();
                if (!File.Exists(path))
                    return string.Empty;

                return File.ReadAllText(path, Encoding.UTF8);
            }
        }

        public static void ClearTodayLog()
        {
            lock (_logLock)
            {
                string path = GetTodayLogPath();
                File.WriteAllText(path, string.Empty, Encoding.UTF8);
            }
        }
    }

    public class ClientPanel : UserControl
    {
        public BotController Bot { get; private set; }
        private CharacterProfile _profile;
        private string _loadedProfileChar = string.Empty;
        private string _lastLoggedEvent = string.Empty;

        private Label lblStats, lblPos, lblBuffs, lblStatus, lblGoalSummary;
        private TextBox txtDailyLog;
        private CheckBox chkEnabled, chkAutoCultivation, chkGroupMobPull, chkDungeonMode;
        private Button btnOpenSettings, btnOpenGoals, btnInviteOthers, btnQuests, btnOpenJson, btnReloadJson;
        private Button btnWhoAmI, btnSelfCultivation, btnCombatBuffs, btnOpenDailyLog, btnClearDailyLog;

        public ClientPanel(BotController bot)
        {
            Bot = bot;
            Dock = DockStyle.Fill;
            AutoScroll = true;
            AutoScrollMinSize = new Size(510, 485);

            BuildLayout();
            LoadCharProfile(Bot.Player.Name);
            RefreshDailyLogView();
        }

        private void BuildLayout()
        {
            Padding = new Padding(12);

            lblStats = new Label
            {
                Location = new Point(12, 12),
                Size = new Size(480, 22),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Text = "HP: 0/0 | MP: 0/0"
            };

            lblPos = new Label
            {
                Location = new Point(12, 36),
                Size = new Size(480, 20),
                Text = "Mapa: 0, 0"
            };

            lblBuffs = new Label
            {
                Location = new Point(12, 58),
                Size = new Size(480, 20),
                Text = "Buffs (IconID): Nenhum"
            };

            lblStatus = new Label
            {
                Location = new Point(12, 82),
                Size = new Size(480, 22),
                ForeColor = Color.DarkBlue,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Text = "Estado: Aguardando"
            };

            lblGoalSummary = new Label
            {
                Location = new Point(12, 106),
                Size = new Size(480, 22),
                ForeColor = Color.DimGray,
                Text = "Meta: sem área fixa"
            };

            chkEnabled = new CheckBox
            {
                Location = new Point(12, 132),
                Size = new Size(115, 24),
                Text = "LIGAR BOT",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.DarkGreen
            };
            chkEnabled.CheckedChanged += (s, e) =>
            {
                Bot.IsEnabled = chkEnabled.Checked;
                AppendLog(chkEnabled.Checked ? "Bot LIGADO." : "Bot DESLIGADO.");
            };

            chkAutoCultivation = new CheckBox
            {
                Location = new Point(132, 134),
                Size = new Size(125, 22),
                Text = "Auto Cultivo",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            chkAutoCultivation.CheckedChanged += (s, e) => ToggleAutoCultivation(chkAutoCultivation.Checked);

            chkGroupMobPull = new CheckBox
            {
                Location = new Point(262, 134),
                Size = new Size(135, 22),
                Text = "Mobada em Grupo",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            chkGroupMobPull.CheckedChanged += (s, e) => ToggleGroupMobPull(chkGroupMobPull.Checked);

            chkDungeonMode = new CheckBox
            {
                Location = new Point(402, 134),
                Size = new Size(92, 22),
                Text = "Modo DG",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            chkDungeonMode.CheckedChanged += (s, e) =>
            {
                Bot.IsInDungeon = chkDungeonMode.Checked;
                AppendLog(chkDungeonMode.Checked ? "Modo DG / Boss ATIVADO." : "Modo DG / Boss DESATIVADO.");
            };

            btnOpenSettings = new Button
            {
                Location = new Point(12, 164),
                Size = new Size(155, 30),
                Text = "Configurações",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnOpenSettings.Click += (s, e) =>
            {
                if (_profile == null) _profile = ProfileManager.LoadForChar(Bot.Player.Name);
                using (var dlg = new BotSettingsForm(Bot, _profile, () => ApplyProfileToEngine()))
                {
                    dlg.ShowDialog(this);
                }
            };

            btnOpenGoals = new Button
            {
                Location = new Point(174, 164),
                Size = new Size(155, 30),
                Text = "Metas",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnOpenGoals.Click += (s, e) =>
            {
                if (_profile == null) _profile = ProfileManager.LoadForChar(Bot.Player.Name);
                using (var dlg = new BotGoalsForm(Bot, _profile, () => ApplyProfileToEngine()))
                {
                    dlg.ShowDialog(this);
                }
            };

            btnInviteOthers = new Button
            {
                Location = new Point(336, 164),
                Size = new Size(154, 30),
                Text = "Chamar Bots Agora"
            };
            btnInviteOthers.Click += (s, e) =>
            {
                foreach (var other in BotHub.GetSnapshot())
                {
                    if (other?.Player != null && other.Player.Id != 0 && other.Player.Id != Bot.Player.Id)
                        Bot.Party.Invite(other.Player.Id);
                }
                AppendLog("Convite de grupo enviado para outros bots conectados.");
            };

            btnQuests = new Button
            {
                Location = new Point(12, 200),
                Size = new Size(235, 28),
                Text = "Ver Quests (Ativas e Disponíveis)",
                BackColor = Color.LightSteelBlue,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnQuests.Click += (s, e) =>
            {
                var questForm = new QuestViewerForm(Bot);
                questForm.Show(this);
            };

            btnOpenJson = new Button
            {
                Location = new Point(253, 200),
                Size = new Size(116, 28),
                Text = "Abrir JSON"
            };
            btnOpenJson.Click += (s, e) =>
            {
                string path = ProfileManager.GetProfilePath(Bot.Player.Name);
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
            };

            btnReloadJson = new Button
            {
                Location = new Point(374, 200),
                Size = new Size(116, 28),
                Text = "Recarregar JSON"
            };
            btnReloadJson.Click += (s, e) => LoadCharProfile(Bot.Player.Name, forceReload: true);

            btnWhoAmI = new Button
            {
                Location = new Point(12, 234),
                Size = new Size(155, 28),
                Text = "WhoAmI (Papel/PT)",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnWhoAmI.Click += (s, e) =>
            {
                uint pid = Bot?.Player?.Id ?? 0u;
                BotEngineModulesUiHelper.OpenWhoAmISettings(this.FindForm() ?? (IWin32Window)this, pid);
                LoadCharProfile(Bot.Player.Name, forceReload: true);
            };

            btnSelfCultivation = new Button
            {
                Location = new Point(174, 234),
                Size = new Size(155, 28),
                Text = "Config. Cultivo",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnSelfCultivation.Click += (s, e) =>
            {
                uint pid = Bot?.Player?.Id ?? 0u;
                BotEngineModulesUiHelper.OpenSelfCultivationSettings(this.FindForm() ?? (IWin32Window)this, pid);
                LoadCharProfile(Bot.Player.Name, forceReload: true);
            };

            btnCombatBuffs = new Button
            {
                Location = new Point(336, 234),
                Size = new Size(154, 28),
                Text = "Buffs / Combate",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnCombatBuffs.Click += (s, e) =>
            {
                uint pid = Bot?.Player?.Id ?? 0u;
                BotEngineModulesUiHelper.OpenCombatBuffSettings(this.FindForm() ?? (IWin32Window)this, pid);
                LoadCharProfile(Bot.Player.Name, forceReload: true);
            };

            btnOpenDailyLog = new Button
            {
                Location = new Point(12, 268),
                Size = new Size(235, 28),
                Text = "Abrir Log do Dia",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnOpenDailyLog.Click += (s, e) =>
            {
                string logPath = DailyBotLogger.GetTodayLogPath();
                if (!File.Exists(logPath))
                    File.WriteAllText(logPath, string.Empty, Encoding.UTF8);

                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{logPath}\"") { UseShellExecute = true });
            };

            btnClearDailyLog = new Button
            {
                Location = new Point(253, 268),
                Size = new Size(237, 28),
                Text = "Limpar Log do Dia",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnClearDailyLog.Click += (s, e) =>
            {
                DailyBotLogger.ClearTodayLog();
                _lastLoggedEvent = string.Empty;
                RefreshDailyLogView();
            };

            txtDailyLog = new TextBox
            {
                Location = new Point(12, 302),
                Size = new Size(478, 155),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = Color.FromArgb(245, 247, 250),
                Font = new Font("Consolas", 8.5f),
                Text = string.Empty
            };

            Controls.AddRange(new Control[]
            {
                lblStats, lblPos, lblBuffs, lblStatus, lblGoalSummary,
                chkEnabled, chkAutoCultivation, chkGroupMobPull, chkDungeonMode,
                btnOpenSettings, btnOpenGoals, btnInviteOthers,
                btnQuests, btnOpenJson, btnReloadJson,
                btnWhoAmI, btnSelfCultivation, btnCombatBuffs,
                btnOpenDailyLog, btnClearDailyLog, txtDailyLog
            });
        }

        private string GetSafeCharKey()
        {
            string name = Bot?.Player?.Name;
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("PID:"))
                return "default";
            return name;
        }

        private void ToggleAutoCultivation(bool enabled)
        {
            Bot.InitializeNewModules();
            string charKey = GetSafeCharKey();
            var cfg = SelfCultivationConfig.LoadForChar(charKey);
            cfg.Enabled = enabled;
            cfg.SaveForChar(charKey);
            Bot.Modules?.LoadForCharacter(charKey, force: true);
            AppendLog(enabled ? "Auto Cultivo ATIVADO." : "Auto Cultivo DESATIVADO.");
        }

        private void ToggleGroupMobPull(bool enabled)
        {
            if (Bot.Routines != null)
                Bot.Routines.AutoMobPull = enabled;

            if (Bot.Combat != null)
                Bot.Combat.HelpAlliesInQuest = enabled;

            if (_profile != null)
            {
                _profile.AutoMobPull = enabled;
                _profile.HelpAlliesInQuest = enabled;
                ProfileManager.SaveForChar(Bot.Player.Name, _profile);
            }

            AppendLog(enabled ? "Mobada em Grupo ATIVADA." : "Mobada em Grupo DESATIVADA.");
        }

        private void AppendLog(string message)
        {
            string charName = Bot?.Player?.Name ?? "Bot";
            DailyBotLogger.Append(charName, message);
            RefreshDailyLogView();
        }

        private void RefreshDailyLogView()
        {
            string content = DailyBotLogger.ReadTodayLog();
            if (txtDailyLog.Text != content)
            {
                txtDailyLog.Text = content;
                txtDailyLog.SelectionStart = txtDailyLog.Text.Length;
                txtDailyLog.ScrollToCaret();
            }
        }

        public void LoadCharProfile(string charName, bool forceReload = false)
        {
            if (!forceReload && _loadedProfileChar == charName) return;

            _loadedProfileChar = charName;
            _profile = ProfileManager.LoadForChar(charName);
            ApplyProfileToEngine();
        }

        private void ApplyProfileToEngine()
        {
            if (_profile == null) return;

            if (Bot.Support != null)
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
            }

            if (Bot.Combat != null)
            {
                Bot.Combat.HelpAlliesInQuest = _profile.HelpAlliesInQuest;
                Bot.Combat.AttackSkillRotation.Clear();
                Bot.Combat.AttackSkillRotation.AddRange(_profile.AttackSkills);
                Bot.Combat.FarmMobTemplateIds.Clear();
                Bot.Combat.FarmMobTemplateIds.AddRange(_profile.FarmMobIds);
                Bot.Combat.FarmCenterX = _profile.AreaCenterX;
                Bot.Combat.FarmCenterY = _profile.AreaCenterY;
                Bot.Combat.FarmCenterZ = _profile.AreaCenterZ;
            }

            if (Bot.Gather != null)
            {
                Bot.Gather.Mode = _profile.LootMode;
                Bot.Gather.LootRadius = _profile.LootRadius;
                Bot.Gather.CollectItemIds.Clear();
                Bot.Gather.CollectItemIds.AddRange(_profile.CollectItemIds);
            }

            if (Bot.Routines != null)
            {
                Bot.Routines.AutoMobPull = _profile.AutoMobPull;
                Bot.Routines.MobPullRadius = _profile.MobPullRadius;
                Bot.Routines.AutoDefend = _profile.AutoDefend;
                Bot.Routines.AggroDetectRadius = _profile.AggroDetectRadius;
                Bot.Routines.HasAreaCenter = _profile.HasAreaCenter;
                Bot.Routines.AreaCenterX = _profile.AreaCenterX;
                Bot.Routines.AreaCenterY = _profile.AreaCenterY;
                Bot.Routines.AreaCenterZ = _profile.AreaCenterZ;
                Bot.Routines.AreaRadius = _profile.AreaRadius;
                Bot.Routines.UseFlightForLongDistance = _profile.UseFlightForLongDistance;
                Bot.Routines.FlyMinDistance = _profile.FlyMinDistance;
                Bot.Routines.FlyAltitude = _profile.FlyAltitude;
            }

            string charKey = GetSafeCharKey();
            var cultCfg = SelfCultivationConfig.LoadForChar(charKey);

            chkGroupMobPull.Checked = _profile.AutoMobPull && _profile.HelpAlliesInQuest;
            chkAutoCultivation.Checked = cultCfg.Enabled;
            chkDungeonMode.Checked = Bot.IsInDungeon;

            lblGoalSummary.Text = _profile.HasAreaCenter
                ? $"Área: ({PlayerCore.ToDisplayX(_profile.AreaCenterX):F0}, {PlayerCore.ToDisplayZ(_profile.AreaCenterZ):F0}) | Raio: {_profile.AreaRadius:F0}m | Mobada: {(_profile.AutoMobPull ? "ON" : "OFF")}"
                : $"Área: livre | Raio: {_profile.AreaRadius:F0}m | Mobada: {(_profile.AutoMobPull ? "ON" : "OFF")}";
        }

        public void UpdateTick()
        {
            if (Bot?.Memory?.Process == null || Bot.Memory.Process.HasExited) return;

            var p = Bot.Player;
            string currentName = p.Name;
            if (_loadedProfileChar != currentName && !currentName.StartsWith("PID:"))
                LoadCharProfile(currentName);

            lblStats.Text = $"{currentName} (Nv. {p.Level})  |  HP: {p.HP}/{p.HPMax}  |  MP: {p.MP}/{p.MPMax}";
            lblPos.Text = $"Mapa: {PlayerCore.ToDisplayX(p.X):F1}, {PlayerCore.ToDisplayZ(p.Z):F1} (Alt: {PlayerCore.ToDisplayY(p.Y):F1})  |  Região: {p.CurrentRegionName}";

            var buffs = Bot.Buffs.GetActiveBuffs();
            lblBuffs.Text = "Buffs (IconID): " + (buffs.Count > 0 ? string.Join(", ", buffs.Select(b => b.IconId)) : "Nenhum");

            string currentEvent = $"[{Bot.CurrentState}] {Bot.StatusText}";
            lblStatus.Text = $"Estado: {currentEvent}";

            if (Bot.IsEnabled && !string.IsNullOrWhiteSpace(Bot.StatusText) && currentEvent != _lastLoggedEvent)
            {
                _lastLoggedEvent = currentEvent;
                DailyBotLogger.Append(currentName, currentEvent);
            }

            RefreshDailyLogView();
        }
    }
}