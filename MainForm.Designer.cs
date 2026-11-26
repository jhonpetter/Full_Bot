using System.Windows.Forms;

namespace Full_Bot
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tmrSync = new System.Windows.Forms.Timer(this.components);
            this.tabMain = new System.Windows.Forms.TabControl();
            this.tabGeneralInfo = new System.Windows.Forms.TabPage();
            this.gridGeneral = new System.Windows.Forms.DataGridView();
            this.colPid = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colChar = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colHp = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMp = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRegion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabMain.SuspendLayout();
            this.tabGeneralInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridGeneral)).BeginInit();
            this.SuspendLayout();

            // tmrSync
            this.tmrSync.Enabled = true;
            this.tmrSync.Interval = 1000;
            this.tmrSync.Tick += new System.EventHandler(this.tmrSync_Tick);

            // tabMain
            this.tabMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMain.Location = new System.Drawing.Point(0, 0);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(780, 460);
            this.tabMain.TabIndex = 0;
            this.tabMain.Controls.Add(this.tabGeneralInfo);

            // tabGeneralInfo
            this.tabGeneralInfo.Location = new System.Drawing.Point(4, 22);
            this.tabGeneralInfo.Name = "tabGeneralInfo";
            this.tabGeneralInfo.Padding = new System.Windows.Forms.Padding(6);
            this.tabGeneralInfo.Size = new System.Drawing.Size(772, 434);
            this.tabGeneralInfo.TabIndex = 0;
            this.tabGeneralInfo.Text = "Informações Gerais";
            this.tabGeneralInfo.UseVisualStyleBackColor = true;
            this.tabGeneralInfo.Controls.Add(this.gridGeneral);

            // gridGeneral
            this.gridGeneral.AllowUserToAddRows = false;
            this.gridGeneral.AllowUserToDeleteRows = false;
            this.gridGeneral.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridGeneral.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridGeneral.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colPid,
                this.colChar,
                this.colLevel,
                this.colHp,
                this.colMp,
                this.colRegion
            });
            this.gridGeneral.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridGeneral.Location = new System.Drawing.Point(6, 6);
            this.gridGeneral.Name = "gridGeneral";
            this.gridGeneral.ReadOnly = true;
            this.gridGeneral.RowHeadersVisible = false;
            this.gridGeneral.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridGeneral.Size = new System.Drawing.Size(760, 422);
            this.gridGeneral.TabIndex = 0;

            // colPid
            this.colPid.HeaderText = "PID";
            this.colPid.Name = "colPid";
            this.colPid.FillWeight = 60F;

            // colChar
            this.colChar.HeaderText = "Personagem";
            this.colChar.Name = "colChar";
            this.colChar.FillWeight = 130F;

            // colLevel
            this.colLevel.HeaderText = "Nv.";
            this.colLevel.Name = "colLevel";
            this.colLevel.FillWeight = 55F;

            // colHp
            this.colHp.HeaderText = "HP";
            this.colHp.Name = "colHp";
            this.colHp.FillWeight = 95F;

            // colMp
            this.colMp.HeaderText = "MP";
            this.colMp.Name = "colMp";
            this.colMp.FillWeight = 95F;

            // colRegion
            this.colRegion.HeaderText = "Região";
            this.colRegion.Name = "colRegion";
            this.colRegion.FillWeight = 145F;

            // MainForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(780, 460);
            this.Controls.Add(this.tabMain);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Full_Bot";

            this.tabMain.ResumeLayout(false);
            this.tabGeneralInfo.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridGeneral)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Timer tmrSync;
        private TabControl tabMain;
        private TabPage tabGeneralInfo;
        private DataGridView gridGeneral;
        private DataGridViewTextBoxColumn colPid;
        private DataGridViewTextBoxColumn colChar;
        private DataGridViewTextBoxColumn colLevel;
        private DataGridViewTextBoxColumn colHp;
        private DataGridViewTextBoxColumn colMp;
        private DataGridViewTextBoxColumn colRegion;
    }
}