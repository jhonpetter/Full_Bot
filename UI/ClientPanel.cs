using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Full_Bot
{
    public class ClientPanel : UserControl
    {
        public BotController Bot { get; private set; }
        private CharacterProfile _profile;
        private string _loadedProfileChar = string.Empty;

        private Label lblStats, lblPos, lblBuffs, lblStatus, lblGoalSummary;
        private TextBox txtTargetLog;
        private CheckBox chkEnabled;
        private Button btnOpenSettings, btnOpenGoals, btnInviteOthers, btnQuests, btnOpenJson, btnReloadJson;
        private Button btnWhoAmI, btnSelfCultivation, btnCombatBuffs;

        public ClientPanel(BotController bot)
        {
            Bot = bot;
            Dock = DockStyle.Fill;
            AutoScroll = true;
            AutoScrollMinSize = new Size(500, 385);

            BuildLayout();
            LoadCharProfile(Bot.Player.Name);
        }

        private void BuildLayout()
        {
            Padding = new Padding(12);

            lblStats = new Label
            {
                Location = new Point(12, 12),
                Size = new Size(470, 22),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Text = "HP: 0/0 | MP: 0/0"
            };

            lblPos = new Label
            {
                Location = new Point(12, 36),
                Size = new Size(470, 20),
                Text = "Mapa: 0, 0"
            };

            lblBuffs = new Label
            {
                Location = new Point(12, 58),
                Size = new Size(470, 20),
                Text = "Buffs (IconID): Nenhum"
            };

            lblStatus = new Label
            {
                Location = new Point(12, 82),
                Size = new Size(470, 22),
                ForeColor = Color.DarkBlue,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Text = "Estado: Aguardando"
            };

            lblGoalSummary = new Label
            {
                Location = new Point(12, 106),
                Size = new Size(470, 22),
                ForeColor = Color.DimGray,
                Text = "Meta: sem área fixa"
            };

            txtTargetLog = new TextBox
            {
                Location = new Point(12, 132),
                Size = new Size(461, 44),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(245, 247, 250),
                Font = new Font("Consolas", 8.5f),
                Text = "Log Target: aguardando..."
            };

            chkEnabled = new CheckBox
            {
                Location = new Point(12, 184),
                Size = new Size(135, 26),
                Text = "LIGAR BOT",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.DarkGreen
            };
            chkEnabled.CheckedChanged += (s, e) => { Bot.IsEnabled = chkEnabled.Checked; };

            btnOpenSettings = new Button
            {
                Location = new Point(155, 182),
                Size = new Size(155, 32),
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
                Location = new Point(318, 182),
                Size = new Size(155, 32),
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
                Location = new Point(12, 224),
                Size = new Size(150, 30),
                Text = "Chamar Bots Agora"
            };
            btnInviteOthers.Click += (s, e) =>
            {
                foreach (var other in BotHub.GetSnapshot())
                {
                    if (other?.Player != null && other.Player.Id != 0 && other.Player.Id != Bot.Player.Id)
                        Bot.Party.Invite(other.Player.Id);
                }
            };

            btnQuests = new Button
            {
                Location = new Point(170, 224),
                Size = new Size(303, 30),
                Text = "Ver Quests (Ativas e Disponíveis)",
                BackColor = Color.LightSteelBlue,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnQuests.Click += (s, e) =>
            {
                var questForm = new QuestViewerForm(Bot);
                questForm.Show(this);
            };

            btnOpenJson = new Button
            {
                Location = new Point(12, 264),
                Size = new Size(150, 28),
                Text = "Abrir JSON do Char"
            };
            btnOpenJson.Click += (s, e) =>
            {
                string path = ProfileManager.GetProfilePath(Bot.Player.Name);
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
            };

            btnReloadJson = new Button
            {
                Location = new Point(170, 264),
                Size = new Size(150, 28),
                Text = "Recarregar JSON"
            };
            btnReloadJson.Click += (s, e) => LoadCharProfile(Bot.Player.Name, forceReload: true);

            btnWhoAmI = new Button
            {
                Location = new Point(12, 302),
                Size = new Size(150, 30),
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
                Location = new Point(170, 302),
                Size = new Size(150, 30),
                Text = "Auto Cultivo",
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
                Location = new Point(328, 302),
                Size = new Size(145, 30),
                Text = "Buffs / Combate",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnCombatBuffs.Click += (s, e) =>
            {
                uint pid = Bot?.Player?.Id ?? 0u;
                BotEngineModulesUiHelper.OpenCombatBuffSettings(this.FindForm() ?? (IWin32Window)this, pid);
                LoadCharProfile(Bot.Player.Name, forceReload: true);
            };

            Controls.AddRange(new Control[]
            {
                lblStats, lblPos, lblBuffs, lblStatus, lblGoalSummary, txtTargetLog,
                chkEnabled, btnOpenSettings, btnOpenGoals,
                btnInviteOthers, btnQuests, btnOpenJson, btnReloadJson,
                btnWhoAmI, btnSelfCultivation, btnCombatBuffs
            });
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
            lblStatus.Text = $"Estado: [{Bot.CurrentState}] - {Bot.StatusText}";

            if (Bot.Combat != null)
            {
                string diag = Bot.Combat.BuildTargetDiagnostic();
                if (txtTargetLog.Text != diag)
                    txtTargetLog.Text = diag;
            }
        }
    }
}