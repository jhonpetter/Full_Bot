using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Full_Bot
{
    public class PlayerFunctionsTestForm : Form
    {
        private readonly PlayerCore _player;
        private readonly TextBox _txtLog;

        // CraftItemById
        private readonly TextBox _txtCraftItemId;
        private readonly TextBox _txtCraftAmount;

        // Info de Skill específica (por ID)
        private readonly TextBox _txtInspectSkillId;

        public PlayerFunctionsTestForm(PlayerCore player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));

            Text = "Teste: Craft, Skills/Buffs & CloseNpcDialog (Temporário)";
            Size = new Size(820, 620);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var grid = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 6,
                Padding = new Padding(4)
            };

            int row = 0;

            // 1. CraftItemById (Item ID + Qtd opcional, padrão 1)
            _txtCraftItemId = MakeInput("Item ID");
            _txtCraftAmount = MakeInput("1");
            AddRow(grid, row++, "CraftItemById",
                new Control[] { MakeLbl("Item ID:"), _txtCraftItemId, MakeLbl("Qtd:"), _txtCraftAmount },
                "Craftar", () =>
                {
                    uint itemId = ParseUIntOrHex(_txtCraftItemId.Text, "Craft Item ID");
                    uint amount = string.IsNullOrWhiteSpace(_txtCraftAmount.Text)
                        ? 1u
                        : ParseUIntOrHex(_txtCraftAmount.Text, "Craft Qtd");

                    bool ok = _player.CraftItemById(itemId, amount);
                    Log($"CraftItemById(id={itemId} [0x{itemId:X}], qtd={amount}) -> {ok}");
                });

            // 2. Ler info de uma Skill específica (ID, Nível, Cooldown, Range, Categoria, Escopo, Buff)
            _txtInspectSkillId = MakeInput("Skill ID");
            AddRow(grid, row++, "Info Skill (por ID)",
                new Control[] { MakeLbl("Skill ID:"), _txtInspectSkillId },
                "Ler Skill", () =>
                {
                    uint skillId = ParseUIntOrHex(_txtInspectSkillId.Text, "Skill ID");
                    var info = _player.GetSkillInfo(skillId);
                    if (info == null)
                    {
                        Log($"Skill {skillId} não encontrada nas skills aprendidas (range via GetSkillRange: {_player.GetSkillRange(skillId):F2}m).");
                        return;
                    }

                    Log(FormatSkillInfo(info));
                });

            // 3. Listar TODAS as Skills aprendidas com detalhes
            AddRow(grid, row++, "Todas as Skills",
                new Control[] { MakeLbl("Lista completa:") },
                "Listar Skills", () =>
                {
                    List<SkillInfo> skills = _player.GetSkills() ?? new List<SkillInfo>();
                    Log($"=== TOTAL DE SKILLS: {skills.Count} ===");
                    foreach (var s in skills)
                    {
                        if (s != null)
                            Log("  " + FormatSkillInfo(s));
                    }
                });

            // 4. Listar TODAS as Skills de Buff / Cura e Buffs ativos
            AddRow(grid, row++, "Buffs (Skills & Ativos)",
                new Control[] { MakeLbl("Skills Buff + Ativos:") },
                "Listar Buffs", () =>
                {
                    List<SkillInfo> skills = _player.GetSkills() ?? new List<SkillInfo>();
                    var buffSkills = skills.Where(s => s != null && (s.IsBuff || s.IsBuffSkill)).ToList();

                    Log($"=== SKILLS DE BUFF/CURA ({buffSkills.Count}) ===");
                    foreach (var b in buffSkills)
                        Log("  " + FormatSkillInfo(b));

                    var activeBuffs = GetActiveBuffsSummary();
                    Log($"=== BUFFS ATIVOS NO PLAYER: {activeBuffs} ===");
                });

            // 5. Fechar janela / diálogo de NPC (Btn_Close)
            AddRow(grid, row++, "CloseNpcDialogWindow",
                new Control[] { MakeLbl("Fecha diálogo via UI:") },
                "Fechar NPC", () =>
                {
                    bool wasOpen = _player.IsNpcDialogOpen();
                    bool ok = _player.CloseNpcDialogWindow();
                    bool stillOpen = _player.IsNpcDialogOpen();
                    Log($"CloseNpcDialogWindow() -> {ok} (eraAberto={wasOpen}, aindaAberto={stillOpen})");
                });

            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9f)
            };

            mainLayout.Controls.Add(grid, 0, 0);
            mainLayout.Controls.Add(_txtLog, 0, 1);
            Controls.Add(mainLayout);
        }

        private static string FormatSkillInfo(SkillInfo s)
        {
            return $"ID={s.Id} (0x{s.Id:X}) | Lv={s.Level} | Cat={s.Category} | Escopo={s.TargetScope} | " +
                   $"Range={s.Range:F2}m | CD={s.Cooldown}/{s.MaxCooldown} | Magic={s.IsMagic} | " +
                   $"Buff={s.IsBuff} | BuffSkill={s.IsBuffSkill} | Ready={s.IsReady} | Learned={s.IsLearned} | Active={s.IsActive}";
        }

        private string GetActiveBuffsSummary()
        {
            try
            {
                var prop = _player.GetType().GetProperty("ActiveBuffs")
                           ?? _player.GetType().GetProperty("Buffs");
                if (prop != null)
                {
                    var val = prop.GetValue(_player, null);
                    if (val is System.Collections.IEnumerable seq && !(val is string))
                    {
                        var items = new List<string>();
                        foreach (var item in seq)
                            items.Add(item?.ToString() ?? "null");
                        return items.Count > 0 ? string.Join(", ", items) : "nenhum";
                    }
                    if (val != null)
                        return val.ToString();
                }

                var method = _player.GetType().GetMethod("GetActiveBuffs", Type.EmptyTypes)
                             ?? _player.GetType().GetMethod("GetBuffs", Type.EmptyTypes);
                if (method != null)
                {
                    var res = method.Invoke(_player, null);
                    if (res is System.Collections.IEnumerable seq && !(res is string))
                    {
                        var items = new List<string>();
                        foreach (var item in seq)
                            items.Add(item?.ToString() ?? "null");
                        return items.Count > 0 ? string.Join(", ", items) : "nenhum";
                    }
                    if (res != null)
                        return res.ToString();
                }
            }
            catch (Exception ex)
            {
                return $"erro ao ler ({ex.Message})";
            }

            return "sem método/propriedade de buffs ativos no PlayerCore";
        }

        // Helper isolado para plugar o botão na tab do player e comentar/remover fácil depois.
        public static Button AttachTestButton(Control targetTabOrContainer, Func<PlayerCore> playerProvider, Point? location = null)
        {
            if (targetTabOrContainer == null || playerProvider == null)
                return null;

            var btn = new Button
            {
                Text = "Testar Craft / Skills",
                Size = new Size(165, 30),
                Location = location ?? new Point(12, 12)
            };

            btn.Click += (s, e) =>
            {
                var p = playerProvider();
                if (p == null)
                {
                    MessageBox.Show("PlayerCore não disponível no momento.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var dlg = new PlayerFunctionsTestForm(p);
                dlg.Show();
            };

            targetTabOrContainer.Controls.Add(btn);
            return btn;
        }

        private void AddRow(TableLayoutPanel grid, int rowIndex, string title, Control[] inputs, string buttonText, Action action)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblFunc = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 8, 3)
            };
            grid.Controls.Add(lblFunc, 0, rowIndex);

            int col = 1;
            foreach (var ctrl in inputs)
            {
                ctrl.Margin = new Padding(2, 3, 4, 3);
                grid.Controls.Add(ctrl, col++, rowIndex);
            }

            while (col < 5)
            {
                grid.Controls.Add(new Panel { Size = new Size(1, 1) }, col++, rowIndex);
            }

            var btn = new Button
            {
                Text = buttonText,
                AutoSize = true,
                Margin = new Padding(6, 3, 3, 3)
            };
            btn.Click += (s, e) =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log($"ERRO em {title}: {ex.Message}");
                }
            };
            grid.Controls.Add(btn, 5, rowIndex);
        }

        private static Label MakeLbl(string text) => new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

        private static TextBox MakeInput(string placeholder)
        {
            return new TextBox
            {
                Width = 95,
                AccessibleName = placeholder,
                Text = string.Empty
            };
        }

        private static uint ParseUIntOrHex(string raw, string fieldName)
        {
            string s = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(s))
                throw new FormatException($"Campo vazio em {fieldName}");

            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint h))
                    return h;
            }

            if (uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint d))
                return d;

            if (uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hNoPrefix))
                return hNoPrefix;

            throw new FormatException($"Valor uint/hex inválido em {fieldName}: '{raw}'");
        }

        private void Log(string msg)
        {
            if (_txtLog == null || _txtLog.IsDisposed) return;
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}";
            if (_txtLog.InvokeRequired)
                _txtLog.BeginInvoke(new Action(() => _txtLog.AppendText(line)));
            else
                _txtLog.AppendText(line);
        }
    }
}