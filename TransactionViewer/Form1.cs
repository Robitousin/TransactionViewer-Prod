// Form1.cs — version consolidée
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.Diagnostics;    // Process.Start / WaitForInputIdle
using System.IO;             // Path, Directory
using System.Configuration;  // ConfigurationManager.AppSettings
using TransactionViewer.DataAccess;
using TransactionViewer.Models;
using TransactionViewer.Printing;
using TransactionViewer.Services; // CsvExporter + ArchiveService

namespace TransactionViewer
{
    public partial class Form1 : Form
    {
        private List<Transaction> lastPrintedList = null;
        private bool lastPrintedIsFailed = false;

        // --- Lancement programme tiers (NSF) ---
        private const string CHEMIN_CREDIT_EXE = @"C:\v100\Credit.exe";

        private bool LancerProgrammeCreditEtAttendre(int timeoutMs = 4000)
        {
            try
            {
                if (!System.IO.File.Exists(CHEMIN_CREDIT_EXE))
                {
                    MessageBox.Show(
                        $"Programme introuvable : {CHEMIN_CREDIT_EXE}",
                        "Crédit – Non trouvé",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = CHEMIN_CREDIT_EXE,
                    UseShellExecute = true,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(CHEMIN_CREDIT_EXE)
                    // Verb = "runas", // ← décommente si l’appli exige des droits admin (UAC)
                };

                var proc = Process.Start(psi);
                if (proc == null) return false;

                try { proc.WaitForInputIdle(timeoutMs); } catch { /* no-op */ }
                Application.DoEvents();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Erreur lancement 'Credit.exe' : " + ex.Message,
                    "Crédit – Erreur",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            dgvPrelevements.EditMode = DataGridViewEditMode.EditOnEnter;
            dgvNSF.EditMode = DataGridViewEditMode.EditOnEnter;
            dgvExceptions.EditMode = DataGridViewEditMode.EditOnEnter;
            RafraichirOnglets();
        }

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

        private void btnImpressionEnregistrement_Click(object sender, EventArgs e)
        {
            var currentTab = tabControl1.SelectedTab;

            if (currentTab == tabPrelevements)
            {
                dgvPrelevements.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvPrelevements.EndEdit();

                var selectedTx = GetCheckedTransactions(dgvPrelevements, "chkSelectPrelev");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour Prélèvements.");
                    return;
                }

                foreach (var tx in selectedTx)
                    TransactionRepository.UpdatePrelevementDone(tx.TransactionID);

                RafraichirOnglets();
                Application.DoEvents();

                this.BeginInvoke((Action)(() =>
                {
                    PrintByDate(selectedTx, isFailed: false);
                }));

                lastPrintedList = selectedTx;
                lastPrintedIsFailed = false;
            }
            else if (currentTab == tabNSF)
            {
                dgvNSF.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvNSF.EndEdit();

                var selectedTx = GetCheckedTransactions(dgvNSF, "chkSelectNSF");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour NSF.");
                    return;
                }
                if (selectedTx.Any(t => !t.IsPrelevementDone))
                {
                    MessageBox.Show("Impossible de traiter en NSF : pas traité en Prélèvements.");
                    return;
                }

                foreach (var tx in selectedTx)
                    TransactionRepository.UpdateNSFDone(tx.TransactionID);

                RafraichirOnglets();
                Application.DoEvents();

                var creditOk = LancerProgrammeCreditEtAttendre(4000);
                // if (!creditOk) return; // décommente si tu veux bloquer la suite

                try
                {
                    // NSF root: AppSetting sinon profil courant (Documents\TransactionViewer\NSF)
                    var nsfRoot = ConfigurationManager.AppSettings["NsfOutputRoot"];
                    if (string.IsNullOrWhiteSpace(nsfRoot))
                    {
                        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        nsfRoot = Path.Combine(docs, "TransactionViewer", "NSF");
                    }
                    Directory.CreateDirectory(nsfRoot);

                    // Chemin explicite => on n’utilise JAMAIS un défaut codé
                    var outFile = Path.Combine(nsfRoot, $"NSF_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                    string csvPath = CsvExporter.ExportTransactionsToCsvLockedFormat(
                        selectedTx,
                        destinationFilePath: outFile,   // << IMPORTANT
                        dateFormat: "yyyy-MM-dd"
                    );

                    // Archive root: AppSetting sinon profil courant (Documents\TransactionViewer\Archive)
                    var archiveRoot = ConfigurationManager.AppSettings["ArchiveRoot"];
                    if (string.IsNullOrWhiteSpace(archiveRoot))
                    {
                        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        archiveRoot = Path.Combine(docs, "TransactionViewer", "Archive");
                    }
                    Directory.CreateDirectory(archiveRoot);

                    string archivedPath = ArchiveService.MoveToNsfArchive(csvPath, archiveRoot);
                    OuvrirExplorerSurFichier(archivedPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erreur export/archivage NSF : " + ex.Message,
                        "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                this.BeginInvoke((Action)(() =>
                {
                    PrintByDate(selectedTx, isFailed: true);
                }));

                lastPrintedList = selectedTx;
                lastPrintedIsFailed = true;
            }
            else if (currentTab == tabExceptions)
            {
                dgvExceptions.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvExceptions.EndEdit();

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

                RafraichirOnglets();
                Application.DoEvents();

                this.BeginInvoke((Action)(() =>
                {
                    PrintExceptions(selectedTx);
                }));
            }
        }

        private void btnEnregistrementSql_Click(object sender, EventArgs e)
        {
            var currentTab = tabControl1.SelectedTab;

            if (currentTab == tabPrelevements)
            {
                dgvPrelevements.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvPrelevements.EndEdit();

                var selectedTx = GetCheckedTransactions(dgvPrelevements, "chkSelectPrelev");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour Prélèvements.");
                    return;
                }

                foreach (var tx in selectedTx)
                    TransactionRepository.UpdatePrelevementDone(tx.TransactionID);
            }
            else if (currentTab == tabNSF)
            {
                dgvNSF.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvNSF.EndEdit();

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
                    TransactionRepository.UpdateNSFDone(tx.TransactionID);
            }
            else if (currentTab == tabExceptions)
            {
                dgvExceptions.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvExceptions.EndEdit();

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
            PrintByDate(lastPrintedList, lastPrintedIsFailed);
        }

        private void RafraichirOnglets()
        {
            var prel = TransactionRepository.GetPrelevements();
            dgvPrelevements.DataSource = prel;

            var nsf = TransactionRepository.GetNSF();
            dgvNSF.DataSource = nsf;

            var exc = TransactionRepository.GetExceptions();
            dgvExceptions.DataSource = exc;
        }

        private List<Transaction> GetCheckedTransactions(DataGridView dgv, string checkboxColumnName)
        {
            var list = new List<Transaction>();
            var allTx = dgv.DataSource as List<Transaction>;
            if (allTx == null) return list;

            foreach (DataGridViewRow row in dgv.Rows)
            {
                bool isChecked = false;
                if (row.Cells[checkboxColumnName].Value != null)
                    bool.TryParse(row.Cells[checkboxColumnName].Value.ToString(), out isChecked);

                if (isChecked)
                {
                    int idx = row.Index;
                    if (idx >= 0 && idx < allTx.Count)
                        list.Add(allTx[idx]);
                }
            }
            return list;
        }

        private void PrintByDate(List<Transaction> txList, bool isFailed)
        {
            if (txList == null || txList.Count == 0)
            {
                MessageBox.Show("Aucune transaction sélectionnée, rien à imprimer.");
                return;
            }

            if (isFailed)
            {
                var grouped = txList
                    .Where(t => DateTime.TryParse(t.LastModified, out var _))
                    .GroupBy(t => { DateTime.TryParse(t.LastModified, out var d); return d.Date; })
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
                        if (diag.ShowDialog() == DialogResult.OK) pd.Print();
                    }
                }
            }
            else
            {
                var grouped = txList
                    .Where(t => DateTime.TryParse(t.TransactionDateTime, out var _))
                    .GroupBy(t => { DateTime.TryParse(t.TransactionDateTime, out var d); return d.Date; })
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
                        if (diag.ShowDialog() == DialogResult.OK) pd.Print();
                    }
                }
            }
        }

        private void PrintExceptions(List<Transaction> txList)
        {
            if (txList == null || txList.Count == 0) return;

            PrintDocument pd = new PrintDocument();
            pd.DefaultPageSettings.Landscape = true;

            var mgr = new PrintManagerException(txList);
            pd.PrintPage += mgr.PrintDocument_PrintPage;

            using (PrintDialog diag = new PrintDialog())
            {
                diag.Document = pd;
                if (diag.ShowDialog() == DialogResult.OK) pd.Print();
            }
        }

        private void chkSelectAllPrelev_CheckedChanged(object sender, EventArgs e)
        {
            SetAllRowsChecked(dgvPrelevements, chkSelectAllPrelev.Checked, "chkSelectPrelev");
        }

        private void chkSelectAllNSF_CheckedChanged(object sender, EventArgs e)
        {
            SetAllRowsChecked(dgvNSF, chkSelectAllNSF.Checked, "chkSelectNSF");
        }

        private void SetAllRowsChecked(DataGridView dgv, bool check, string checkboxColumnName)
        {
            dgv.EndEdit();
            foreach (DataGridViewRow row in dgv.Rows)
                row.Cells[checkboxColumnName].Value = check;
            dgv.EndEdit();
            dgv.Refresh();
        }

        private void dgvPrelevements_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvPrelevements.Columns[e.ColumnIndex].Name == "chkSelectPrelev")
                dgvPrelevements.EndEdit();
        }

        private void dgvNSF_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvNSF.Columns[e.ColumnIndex].Name == "chkSelectNSF")
                dgvNSF.EndEdit();
        }

        private void OuvrirExplorerSurFichier(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + fullPath + "\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch
            {
                try
                {
                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = dir,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                }
                catch { /* no-op */ }
            }
        }
    }
}
