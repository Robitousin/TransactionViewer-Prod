namespace TransactionViewer
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Nettoyage des ressources.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Code généré par le Concepteur Windows Form

        private void InitializeComponent()
        {
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPrelevements = new System.Windows.Forms.TabPage();
            this.chkSelectAllPrelev = new System.Windows.Forms.CheckBox();
            this.dgvPrelevements = new System.Windows.Forms.DataGridView();
            this.chkSelectPrelev = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.tabNSF = new System.Windows.Forms.TabPage();
            this.chkSelectAllNSF = new System.Windows.Forms.CheckBox();
            this.dgvNSF = new System.Windows.Forms.DataGridView();
            this.chkSelectNSF = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.tabExceptions = new System.Windows.Forms.TabPage();
            this.chkSelectAllExcept = new System.Windows.Forms.CheckBox();
            this.dgvExceptions = new System.Windows.Forms.DataGridView();
            this.chkSelectExc = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.btnImporter = new System.Windows.Forms.Button();
            this.btnRafraichir = new System.Windows.Forms.Button();
            this.btnImpressionEnregistrement = new System.Windows.Forms.Button();
            this.btnEnregistrementSql = new System.Windows.Forms.Button();
            this.btnReimprimerDerniere = new System.Windows.Forms.Button();
            this.tabControl1.SuspendLayout();
            this.tabPrelevements.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrelevements)).BeginInit();
            this.tabNSF.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvNSF)).BeginInit();
            this.tabExceptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvExceptions)).BeginInit();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPrelevements);
            this.tabControl1.Controls.Add(this.tabNSF);
            this.tabControl1.Controls.Add(this.tabExceptions);
            this.tabControl1.Location = new System.Drawing.Point(12, 41);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1303, 585);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPrelevements
            // 
            this.tabPrelevements.Controls.Add(this.chkSelectAllPrelev);
            this.tabPrelevements.Controls.Add(this.dgvPrelevements);
            this.tabPrelevements.Location = new System.Drawing.Point(4, 22);
            this.tabPrelevements.Name = "tabPrelevements";
            this.tabPrelevements.Padding = new System.Windows.Forms.Padding(3);
            this.tabPrelevements.Size = new System.Drawing.Size(1295, 559);
            this.tabPrelevements.TabIndex = 0;
            this.tabPrelevements.Text = "Prélèvements";
            this.tabPrelevements.UseVisualStyleBackColor = true;
            // 
            // chkSelectAllPrelev
            // 
            this.chkSelectAllPrelev.AutoSize = true;
            this.chkSelectAllPrelev.Location = new System.Drawing.Point(6, 6);
            this.chkSelectAllPrelev.Name = "chkSelectAllPrelev";
            this.chkSelectAllPrelev.Size = new System.Drawing.Size(70, 17);
            this.chkSelectAllPrelev.TabIndex = 1;
            this.chkSelectAllPrelev.Text = "Select All";
            this.chkSelectAllPrelev.UseVisualStyleBackColor = true;
            this.chkSelectAllPrelev.CheckedChanged += new System.EventHandler(this.chkSelectAllPrelev_CheckedChanged);
            // 
            // dgvPrelevements
            // 
            this.dgvPrelevements.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPrelevements.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.chkSelectPrelev});
            this.dgvPrelevements.Location = new System.Drawing.Point(9, 35);
            this.dgvPrelevements.Name = "dgvPrelevements";
            this.dgvPrelevements.Size = new System.Drawing.Size(1283, 524);
            this.dgvPrelevements.TabIndex = 0;
            this.dgvPrelevements.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvPrelevements_CellContentClick);
            // 
            // chkSelectPrelev
            // 
            this.chkSelectPrelev.HeaderText = "Select";
            this.chkSelectPrelev.Name = "chkSelectPrelev";
            // 
            // tabNSF
            // 
            this.tabNSF.Controls.Add(this.chkSelectAllNSF);
            this.tabNSF.Controls.Add(this.dgvNSF);
            this.tabNSF.Location = new System.Drawing.Point(4, 22);
            this.tabNSF.Name = "tabNSF";
            this.tabNSF.Padding = new System.Windows.Forms.Padding(3);
            this.tabNSF.Size = new System.Drawing.Size(1295, 559);
            this.tabNSF.TabIndex = 1;
            this.tabNSF.Text = "NSF";
            this.tabNSF.UseVisualStyleBackColor = true;
            // 
            // chkSelectAllNSF
            // 
            this.chkSelectAllNSF.AutoSize = true;
            this.chkSelectAllNSF.Location = new System.Drawing.Point(6, 6);
            this.chkSelectAllNSF.Name = "chkSelectAllNSF";
            this.chkSelectAllNSF.Size = new System.Drawing.Size(70, 17);
            this.chkSelectAllNSF.TabIndex = 1;
            this.chkSelectAllNSF.Text = "Select All";
            this.chkSelectAllNSF.UseVisualStyleBackColor = true;
            this.chkSelectAllNSF.CheckedChanged += new System.EventHandler(this.chkSelectAllNSF_CheckedChanged);
            // 
            // dgvNSF
            // 
            this.dgvNSF.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvNSF.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.chkSelectNSF});
            this.dgvNSF.Location = new System.Drawing.Point(6, 29);
            this.dgvNSF.Name = "dgvNSF";
            this.dgvNSF.Size = new System.Drawing.Size(1283, 524);
            this.dgvNSF.TabIndex = 0;
            this.dgvNSF.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvNSF_CellContentClick);
            // 
            // chkSelectNSF
            // 
            this.chkSelectNSF.HeaderText = "Select";
            this.chkSelectNSF.Name = "chkSelectNSF";
            // 
            // tabExceptions
            // 
            this.tabExceptions.Controls.Add(this.chkSelectAllExcept);
            this.tabExceptions.Controls.Add(this.dgvExceptions);
            this.tabExceptions.Location = new System.Drawing.Point(4, 22);
            this.tabExceptions.Name = "tabExceptions";
            this.tabExceptions.Padding = new System.Windows.Forms.Padding(3);
            this.tabExceptions.Size = new System.Drawing.Size(1295, 559);
            this.tabExceptions.TabIndex = 2;
            this.tabExceptions.Text = "Exceptions";
            this.tabExceptions.UseVisualStyleBackColor = true;
            // 
            // chkSelectAllExcept
            // 
            this.chkSelectAllExcept.AutoSize = true;
            this.chkSelectAllExcept.Location = new System.Drawing.Point(6, 6);
            this.chkSelectAllExcept.Name = "chkSelectAllExcept";
            this.chkSelectAllExcept.Size = new System.Drawing.Size(70, 17);
            this.chkSelectAllExcept.TabIndex = 1;
            this.chkSelectAllExcept.Text = "Select All";
            this.chkSelectAllExcept.UseVisualStyleBackColor = true;
            // 
            // dgvExceptions
            // 
            this.dgvExceptions.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvExceptions.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.chkSelectExc});
            this.dgvExceptions.Location = new System.Drawing.Point(6, 29);
            this.dgvExceptions.Name = "dgvExceptions";
            this.dgvExceptions.Size = new System.Drawing.Size(1283, 524);
            this.dgvExceptions.TabIndex = 0;
            // 
            // chkSelectExc
            // 
            this.chkSelectExc.HeaderText = "Select";
            this.chkSelectExc.Name = "chkSelectExc";
            // 
            // btnImporter
            // 
            this.btnImporter.Location = new System.Drawing.Point(22, 12);
            this.btnImporter.Name = "btnImporter";
            this.btnImporter.Size = new System.Drawing.Size(87, 23);
            this.btnImporter.TabIndex = 1;
            this.btnImporter.Text = "Importer JSON";
            this.btnImporter.UseVisualStyleBackColor = true;
            this.btnImporter.Click += new System.EventHandler(this.btnImporter_Click);
            // 
            // btnRafraichir
            // 
            this.btnRafraichir.Location = new System.Drawing.Point(337, 12);
            this.btnRafraichir.Name = "btnRafraichir";
            this.btnRafraichir.Size = new System.Drawing.Size(82, 23);
            this.btnRafraichir.TabIndex = 2;
            this.btnRafraichir.Text = "Rafraichir";
            this.btnRafraichir.UseVisualStyleBackColor = true;
            this.btnRafraichir.Click += new System.EventHandler(this.btnRafraichir_Click);
            // 
            // btnImpressionEnregistrement
            // 
            this.btnImpressionEnregistrement.Location = new System.Drawing.Point(210, 12);
            this.btnImpressionEnregistrement.Name = "btnImpressionEnregistrement";
            this.btnImpressionEnregistrement.Size = new System.Drawing.Size(100, 23);
            this.btnImpressionEnregistrement.TabIndex = 3;
            this.btnImpressionEnregistrement.Text = "Print / Inscrire";
            this.btnImpressionEnregistrement.UseVisualStyleBackColor = true;
            this.btnImpressionEnregistrement.Click += new System.EventHandler(this.btnImpressionEnregistrement_Click);
            // 
            // btnEnregistrementSql
            // 
            this.btnEnregistrementSql.Location = new System.Drawing.Point(1089, 12);
            this.btnEnregistrementSql.Name = "btnEnregistrementSql";
            this.btnEnregistrementSql.Size = new System.Drawing.Size(106, 23);
            this.btnEnregistrementSql.TabIndex = 4;
            this.btnEnregistrementSql.Text = "No Print / Inscrire";
            this.btnEnregistrementSql.UseVisualStyleBackColor = true;
            this.btnEnregistrementSql.Click += new System.EventHandler(this.btnEnregistrementSql_Click);
            // 
            // btnReimprimerDerniere
            // 
            this.btnReimprimerDerniere.Location = new System.Drawing.Point(1223, 12);
            this.btnReimprimerDerniere.Name = "btnReimprimerDerniere";
            this.btnReimprimerDerniere.Size = new System.Drawing.Size(76, 23);
            this.btnReimprimerDerniere.TabIndex = 5;
            this.btnReimprimerDerniere.Text = "Dernier Print";
            this.btnReimprimerDerniere.UseVisualStyleBackColor = true;
            this.btnReimprimerDerniere.Click += new System.EventHandler(this.btnReimprimerDerniere_Click);
            // 
            // Form1
            // 
            this.ClientSize = new System.Drawing.Size(1323, 632);
            this.Controls.Add(this.btnReimprimerDerniere);
            this.Controls.Add(this.btnEnregistrementSql);
            this.Controls.Add(this.btnImpressionEnregistrement);
            this.Controls.Add(this.btnRafraichir);
            this.Controls.Add(this.btnImporter);
            this.Controls.Add(this.tabControl1);
            this.Name = "Form1";
            this.Text = "TransactionViewer 0.0.22";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPrelevements.ResumeLayout(false);
            this.tabPrelevements.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrelevements)).EndInit();
            this.tabNSF.ResumeLayout(false);
            this.tabNSF.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvNSF)).EndInit();
            this.tabExceptions.ResumeLayout(false);
            this.tabExceptions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvExceptions)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPrelevements;
        private System.Windows.Forms.TabPage tabNSF;
        private System.Windows.Forms.DataGridView dgvPrelevements;
        private System.Windows.Forms.DataGridView dgvNSF;
        private System.Windows.Forms.Button btnImporter;
        private System.Windows.Forms.Button btnRafraichir;
        private System.Windows.Forms.Button btnImpressionEnregistrement;
        private System.Windows.Forms.Button btnEnregistrementSql;
        private System.Windows.Forms.Button btnReimprimerDerniere;
        private System.Windows.Forms.CheckBox chkSelectAllPrelev;
        private System.Windows.Forms.CheckBox chkSelectAllNSF;
        private System.Windows.Forms.DataGridViewCheckBoxColumn chkSelectPrelev;
        private System.Windows.Forms.DataGridViewCheckBoxColumn chkSelectNSF;
        private System.Windows.Forms.TabPage tabExceptions;
        private System.Windows.Forms.CheckBox chkSelectAllExcept;
        private System.Windows.Forms.DataGridView dgvExceptions;
        private System.Windows.Forms.DataGridViewCheckBoxColumn chkSelectExc;
    }
}





