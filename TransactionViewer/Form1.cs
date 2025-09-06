using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Printing;
using TransactionViewer.DataAccess;
using TransactionViewer.Models;
using TransactionViewer.Printing;

namespace TransactionViewer
{
    public partial class Form1 : Form
    {
        private List<Transaction> lastPrintedList = null;
        private bool lastPrintedIsFailed = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Ajuster l'édition pour cocher les checkbox
            dgvPrelevements.EditMode = DataGridViewEditMode.EditOnEnter;
            dgvNSF.EditMode = DataGridViewEditMode.EditOnEnter;
            dgvExceptions.EditMode = DataGridViewEditMode.EditOnEnter;

            // Chargement initial
            RafraichirOnglets();
        }

        // =====================================
        // = Boutons
        // =====================================

        private void btnImporter_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Fichiers JSON|*.json";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    JsonImportService.ImportTransactionsFromFile(ofd.FileName);
                    RafraichirOnglets();
                }
            }
        }

        private void btnRafraichir_Click(object sender, EventArgs e)
        {
            RafraichirOnglets();
        }

        /// <summary>
        /// Impression + Enregistrement
        /// </summary>
        private void btnImpressionEnregistrement_Click(object sender, EventArgs e)
        {
            var currentTab = tabControl1.SelectedTab;
            if (currentTab == tabPrelevements)
            {
                var selectedTx = GetCheckedTransactions(dgvPrelevements, "chkSelectPrelev");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour Prélèvements.");
                    return;
                }

                // Marquer Prelevement
                foreach (var tx in selectedTx)
                {
                    TransactionRepository.UpdatePrelevementDone(tx.TransactionID);
                }

                // Imprimer (prélèvements => isFailed=false => portrait)
                PrintByDate(selectedTx, isFailed: false);

                lastPrintedList = selectedTx;
                lastPrintedIsFailed = false;
            }
            else if (currentTab == tabNSF)
            {
                var selectedTx = GetCheckedTransactions(dgvNSF, "chkSelectNSF");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour NSF.");
                    return;
                }

                // Vérifier Prélèvement déjà fait
                if (selectedTx.Any(t => !t.IsPrelevementDone))
                {
                    MessageBox.Show("Impossible de traiter en NSF : pas traité en Prélèvements.");
                    return;
                }

                foreach (var tx in selectedTx)
                {
                    TransactionRepository.UpdateNSFDone(tx.TransactionID);
                }

                // Imprimer (NSF => isFailed=true => paysage)
                PrintByDate(selectedTx, isFailed: true);

                lastPrintedList = selectedTx;
                lastPrintedIsFailed = true;
            }
            else if (currentTab == tabExceptions)
            {
                // NOUVEAU : gestion de l'onglet Exceptions
                var selectedTx = GetCheckedTransactions(dgvExceptions, "chkSelectExc");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour Exceptions.");
                    return;
                }

                // 1) Mettre IsVerifier = true, IsException = false
                foreach (var tx in selectedTx)
                {
                    tx.IsVerifier = true;
                    tx.IsException = false;
                    TransactionRepository.InsertOrUpdateTransaction(tx);
                }

                // 2) Imprimer via un manager d'Exception
                PrintExceptions(selectedTx);

                // 3) Rafraîchir
                RafraichirOnglets();
            }
        }

        /// <summary>
        /// Enregistrement SQL sans impression
        /// </summary>
        private void btnEnregistrementSql_Click(object sender, EventArgs e)
        {
            var currentTab = tabControl1.SelectedTab;
            if (currentTab == tabPrelevements)
            {
                var selectedTx = GetCheckedTransactions(dgvPrelevements, "chkSelectPrelev");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour Prélèvements.");
                    return;
                }

                foreach (var tx in selectedTx)
                {
                    TransactionRepository.UpdatePrelevementDone(tx.TransactionID);
                }
            }
            else if (currentTab == tabNSF)
            {
                var selectedTx = GetCheckedTransactions(dgvNSF, "chkSelectNSF");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour NSF.");
                    return;
                }
                if (selectedTx.Any(t => !t.IsPrelevementDone))
                {
                    MessageBox.Show("Impossible d'enregistrer en NSF, pas prélevé.");
                    return;
                }
                foreach (var tx in selectedTx)
                {
                    TransactionRepository.UpdateNSFDone(tx.TransactionID);
                }
            }
            else if (currentTab == tabExceptions)
            {
                // Optionnel si vous voulez un "No Print" pour Exceptions
                var selectedTx = GetCheckedTransactions(dgvExceptions, "chkSelectExc");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour Exceptions.");
                    return;
                }

                foreach (var tx in selectedTx)
                {
                    tx.IsVerifier = true;
                    tx.IsException = false;
                    TransactionRepository.InsertOrUpdateTransaction(tx);
                }
            }

            RafraichirOnglets();
        }

        private void btnReimprimerDerniere_Click(object sender, EventArgs e)
        {
            if (lastPrintedList == null || lastPrintedList.Count == 0)
            {
                MessageBox.Show("Aucune impression précédente.");
                return;
            }
            // On réutilise lastPrintedIsFailed pour savoir si on imprime portrait ou paysage
            PrintByDate(lastPrintedList, lastPrintedIsFailed);
        }

        // =====================================
        // = Méthodes d'aide
        // =====================================

        private void RafraichirOnglets()
        {
            var prel = TransactionRepository.GetPrelevements();
            dgvPrelevements.DataSource = prel;

            var nsf = TransactionRepository.GetNSF();
            dgvNSF.DataSource = nsf;

            // Nouvel onglet "Exceptions"
            var exc = TransactionRepository.GetExceptions();
            dgvExceptions.DataSource = exc;
        }

        /// <summary>
        /// Récupère la liste de transactions cochées dans un DataGridView
        /// en spécifiant le nom de la colonne
        /// </summary>
        private List<Transaction> GetCheckedTransactions(DataGridView dgv, string checkboxColumnName)
        {
            var list = new List<Transaction>();
            var allTx = dgv.DataSource as List<Transaction>;
            if (allTx == null) return list;

            foreach (DataGridViewRow row in dgv.Rows)
            {
                bool isChecked = false;
                if (row.Cells[checkboxColumnName].Value != null)
                {
                    bool.TryParse(row.Cells[checkboxColumnName].Value.ToString(), out isChecked);
                }
                if (isChecked)
                {
                    int idx = row.Index;
                    if (idx >= 0 && idx < allTx.Count)
                    {
                        list.Add(allTx[idx]);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Imprime par date, en portrait pour prélèvements (isFailed=false),
        /// et en paysage pour NSF (isFailed=true)
        /// </summary>
        private void PrintByDate(List<Transaction> txList, bool isFailed)
        {
            if (txList == null || txList.Count == 0)
            {
                MessageBox.Show("Aucune transaction sélectionnée, rien à imprimer.");
                return;
            }

            if (isFailed)
            {
                // => NSF, on imprime en PAYSAGE
                var grouped = txList
                    .Where(t => DateTime.TryParse(t.LastModified, out var dt))
                    .GroupBy(t =>
                    {
                        DateTime.TryParse(t.LastModified, out var dd);
                        return dd.Date;
                    })
                    .OrderBy(g => g.Key);

                if (!grouped.Any())
                {
                    MessageBox.Show("Aucune date valide (LastModified) pour impression NSF.");
                    return;
                }

                foreach (var group in grouped)
                {
                    PrintDocument pd = new PrintDocument();
                    pd.DefaultPageSettings.Landscape = true;

                    var mgr = new PrintManagerFailed(group.ToList());
                    pd.PrintPage += mgr.PrintDocument_PrintPage;

                    using (PrintDialog diag = new PrintDialog())
                    {
                        diag.Document = pd;
                        if (diag.ShowDialog() == DialogResult.OK)
                        {
                            pd.Print();
                        }
                    }
                }
            }
            else
            {
                // => Prélèvements => PORTRAIT
                var grouped = txList
                    .Where(t => DateTime.TryParse(t.TransactionDateTime, out var dt))
                    .GroupBy(t =>
                    {
                        DateTime.TryParse(t.TransactionDateTime, out var dd);
                        return dd.Date;
                    })
                    .OrderBy(g => g.Key);

                if (!grouped.Any())
                {
                    MessageBox.Show("Aucune date valide (TransactionDateTime) pour impression Prélèvements.");
                    return;
                }

                foreach (var group in grouped)
                {
                    PrintDocument pd = new PrintDocument();
                    pd.DefaultPageSettings.Landscape = false;

                    var mgr = new PrintManager(group.ToList());
                    pd.PrintPage += mgr.PrintDocument_PrintPage;

                    using (PrintDialog diag = new PrintDialog())
                    {
                        diag.Document = pd;
                        if (diag.ShowDialog() == DialogResult.OK)
                        {
                            pd.Print();
                        }
                    }
                }
            }
        }

        // Impression pour Exceptions si vous voulez un manager dédié
        private void PrintExceptions(List<Transaction> txList)
        {
            if (txList == null || txList.Count == 0) return;

            // ex. vous pouvez grouper par LastModified ou non
            PrintDocument pd = new PrintDocument();
            pd.DefaultPageSettings.Landscape = true; // ou false, selon votre choix

            var mgr = new PrintManagerException(txList); // si vous avez une classe PrintManagerException
            pd.PrintPage += mgr.PrintDocument_PrintPage;

            using (PrintDialog diag = new PrintDialog())
            {
                diag.Document = pd;
                if (diag.ShowDialog() == DialogResult.OK)
                {
                    pd.Print();
                }
            }
        }

        // =====================================
        // = CheckBox "Cocher Tout"
        // =====================================
        private void chkSelectAllPrelev_CheckedChanged(object sender, EventArgs e)
        {
            SetAllRowsChecked(dgvPrelevements, chkSelectAllPrelev.Checked, "chkSelectPrelev");
        }

        private void chkSelectAllNSF_CheckedChanged(object sender, EventArgs e)
        {
            SetAllRowsChecked(dgvNSF, chkSelectAllNSF.Checked, "chkSelectNSF");
        }

        // Si vous avez un CheckBox "chkSelectAllExc", vous pouvez faire pareil

        private void SetAllRowsChecked(DataGridView dgv, bool check, string checkboxColumnName)
        {
            dgv.EndEdit();
            foreach (DataGridViewRow row in dgv.Rows)
            {
                row.Cells[checkboxColumnName].Value = check;
            }
            dgv.EndEdit();
            dgv.Refresh();
        }

        private void dgvPrelevements_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvPrelevements.Columns[e.ColumnIndex].Name == "chkSelectPrelev")
            {
                dgvPrelevements.EndEdit();
            }
        }

        private void dgvNSF_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvNSF.Columns[e.ColumnIndex].Name == "chkSelectNSF")
            {
                dgvNSF.EndEdit();
            }
        }

        // idem si vous avez un dgvExceptions_CellContentClick
    }
}













