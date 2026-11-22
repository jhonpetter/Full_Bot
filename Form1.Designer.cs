using System.Drawing;
using System.Windows.Forms;

namespace Full_Bot
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tmrUpdate = new System.Windows.Forms.Timer(this.components);
            this.tabClients = new System.Windows.Forms.TabControl();
            this.status = new System.Windows.Forms.TabPage();
            this.gridOverview = new System.Windows.Forms.DataGridView();
            this.Char = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.LVL = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.HP = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MP = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Pos = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabClients.SuspendLayout();
            this.status.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridOverview)).BeginInit();
            this.SuspendLayout();
            // 
            // tmrUpdate
            // 
            this.tmrUpdate.Enabled = true;
            this.tmrUpdate.Interval = 1000;
            this.tmrUpdate.Tick += new System.EventHandler(this.tmrUpdate_Tick);
            // 
            // tabClients
            // 
            this.tabClients.Controls.Add(this.status);
            this.tabClients.Location = new System.Drawing.Point(12, 34);
            this.tabClients.Name = "tabClients";
            this.tabClients.SelectedIndex = 0;
            this.tabClients.Size = new System.Drawing.Size(662, 344);
            this.tabClients.TabIndex = 4;
            // 
            // status
            // 
            this.status.Controls.Add(this.gridOverview);
            this.status.Location = new System.Drawing.Point(4, 22);
            this.status.Name = "status";
            this.status.Padding = new System.Windows.Forms.Padding(3);
            this.status.Size = new System.Drawing.Size(654, 318);
            this.status.TabIndex = 0;
            this.status.Text = "Status";
            this.status.UseVisualStyleBackColor = true;
            // 
            // gridOverview
            // 
            this.gridOverview.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridOverview.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Char,
            this.LVL,
            this.HP,
            this.MP,
            this.Pos});
            this.gridOverview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridOverview.Location = new System.Drawing.Point(3, 3);
            this.gridOverview.Name = "gridOverview";
            this.gridOverview.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.gridOverview.Size = new System.Drawing.Size(648, 312);
            this.gridOverview.TabIndex = 0;
            // 
            // Char
            // 
            this.Char.HeaderText = "Char";
            this.Char.Name = "Char";
            // 
            // LVL
            // 
            this.LVL.HeaderText = "LVL";
            this.LVL.Name = "LVL";
            // 
            // HP
            // 
            this.HP.HeaderText = "HP";
            this.HP.Name = "HP";
            // 
            // MP
            // 
            this.MP.HeaderText = "MP";
            this.MP.Name = "MP";
            // 
            // Pos
            // 
            this.Pos.HeaderText = "Pos";
            this.Pos.Name = "Pos";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(686, 390);
            this.Controls.Add(this.tabClients);
            this.Name = "Form1";
            this.Text = "Full_Bot";
            this.tabClients.ResumeLayout(false);
            this.status.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridOverview)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Timer tmrUpdate;
        private TabControl tabClients;
        private TabPage status;
        private DataGridView gridOverview;
        private DataGridViewTextBoxColumn Char;
        private DataGridViewTextBoxColumn LVL;
        private DataGridViewTextBoxColumn HP;
        private DataGridViewTextBoxColumn MP;
        private DataGridViewTextBoxColumn Pos;
    }
}