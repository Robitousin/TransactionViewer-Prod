// Form1.cs — version corrigée (async/await, sans BeginInvoke depuis l’UI)

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.Diagnostics;    // Process.Start / WaitForInputIdle
using System.IO;             // Path, Directory
using System.Configuration;  // ConfigurationManager.AppSettings
using System.Threading.Tasks;

using TransactionViewer.DataAccess;
using TransactionViewer.Helpers;
using TransactionViewer.Models;
using TransactionViewer.Printing;
using TransactionViewer.Services; // CsvExporter + ArchiveService

namespace TransactionViewer
{
    /// <summary>
    /// Formulaire principal de visualisation et traitement des transactions.
    /// Gère trois flux :
    ///  - Prélèvements (validation + impression portrait)
    ///  - NSF / Échecs (validation + export CSV + impression paysage)
    ///  - Exceptions (marquage comme vérifiées + impression)
    /// Persiste les états via <see cref="TransactionRepository"/>.
    /// </summary>
    public partial class Form1 : Form
    {
        /// <summary>Dernière liste imprimée (tous groupes confondus).</summary>
        private List<Transaction> lastPrintedList = null;
        /// <summary>Indique si la dernière impression concernait des transactions en échec (NSF).</summary>
        private bool lastPrintedIsFailed = false;

        // --- Lancement programme tiers (NSF) ---
        /// <summary>Chemin absolu de l'exécutable externe appelé avant export NSF.</summary>
        private const string CHEMIN_CREDIT_EXE = @"C:\v100\Credit.exe";

        /// <summary>
        /// Lance l'exécutable externe défini dans <see cref="CHEMIN_CREDIT_EXE"/> et attend qu'il soit prêt (idle) ou expiration timeout.
        /// </summary>
        /// <param name="timeoutMs">Durée maximale d'attente (ms) de l'état input idle.</param>
        /// <returns>true si le processus a été démarré correctement, sinon false.</returns>
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
            dgvPrelevements.CellFormatting += Dgv_CellFormatting_ClientRefFallback;
            dgvNSF.CellFormatting += Dgv_CellFormatting_ClientRefFallback;
            dgvExceptions.CellFormatting += Dgv_CellFormatting_ClientRefFallback;

            RafraichirOnglets();
        }

        // =======================
        // Import / Rafraîchir
        // =======================

        private async void btnImporter_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Fichiers JSON|*.json";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        Cursor.Current = Cursors.WaitCursor;
                        await Task.Run(() =>
                        {
                            JsonImportService.ImportTransactionsFromFile(ofd.FileName);
                        });
                    }
                    finally
                    {
                        Cursor.Current = Cursors.Default;
                    }

                    RafraichirOnglets();
                }
            }
        }

        private void btnRafraichir_Click(object sender, EventArgs e)
        {
            RafraichirOnglets();
        }

        private void Dgv_CellFormatting_ClientRefFallback(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null || e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = dgv.Columns[e.ColumnIndex];

            // Cible strictement la colonne "ClientReferenceNumber" (AutoGenerateColumns → DataPropertyName = nom du modèle)
            var isClientRefCol = (col.DataPropertyName ?? col.Name) == "ClientReferenceNumber";
            if (!isClientRefCol) return;

            var tx = dgv.Rows[e.RowIndex].DataBoundItem as Transaction;
            if (tx == null) return;

            var display = ClientRefHelper.GetClientRef(tx);
            if (display != null)
            {
                e.Value = display;
                e.FormattingApplied = true;
            }
        }

        // =======================
        // Impression / Inscription
        // =======================

        private async void btnImpressionEnregistrement_Click(object sender, EventArgs e)
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

                // Impression directe (UI)
                PrintByDate(selectedTx, isFailed: false);

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

                string archivedPath = null;
                try
                {
                    Cursor.Current = Cursors.WaitCursor;

                    // Tâches longues hors UI
                    archivedPath = await Task.Run(() =>
                    {
                        // 1) Lancement programme tiers (et attente Idle)
                        LancerProgrammeCreditEtAttendre(4000);

                        // 2) Export CSV + Archivage
                        var nsfRoot = ConfigurationManager.AppSettings["NsfOutputRoot"];
                        if (string.IsNullOrWhiteSpace(nsfRoot))
                        {
                            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                            nsfRoot = Path.Combine(docs, "TransactionViewer", "NSF");
                        }
                        Directory.CreateDirectory(nsfRoot);

                        var outFile = Path.Combine(nsfRoot, $"NSF_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                        CsvExporter.ExportTransactionsToCsvLockedFormat(
                            selectedTx,
                            destinationFilePath: outFile,
                            dateFormat: "yyyy-MM-dd"
                        );

                        var archiveRoot = ConfigurationManager.AppSettings["ArchiveRoot"];
                        if (string.IsNullOrWhiteSpace(archiveRoot))
                        {
                            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                            archiveRoot = Path.Combine(docs, "TransactionViewer", "Archive");
                        }
                        Directory.CreateDirectory(archiveRoot);

                        return ArchiveService.MoveToNsfArchive(outFile, archiveRoot);
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erreur export/archivage NSF : " + ex.Message,
                        "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }

                // Ouvrir l’explorateur (UI)
                if (!string.IsNullOrWhiteSpace(archivedPath))
                    OuvrirExplorerSurFichier(archivedPath);

                // Impression (UI)
                PrintByDate(selectedTx, isFailed: true);

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

                try
                {
                    Cursor.Current = Cursors.WaitCursor;

                    // Marquage potentiellement long → hors UI
                    await Task.Run(() =>
                    {
                        foreach (var tx in selectedTx)
                        {
                            tx.IsVerifier = true;
                            tx.IsException = false;
                            TransactionRepository.InsertOrUpdateTransaction(tx);
                        }
                    });
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }

                RafraichirOnglets();

                // Impression (UI)
                PrintExceptions(selectedTx);
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

        // =======================
        // Rafraîchir / Aide
        // =======================

        private void RafraichirOnglets()
        {
            var prel = TransactionRepository.GetPrelevements();
            dgvPrelevements.DataSource = prel;
            ApplyUnifiedLayout(dgvPrelevements, "chkSelectPrelev");

            var nsf = TransactionRepository.GetNSF();
            dgvNSF.DataSource = nsf;
            ApplyUnifiedLayout(dgvNSF, "chkSelectNSF");

            var exc = TransactionRepository.GetExceptions();
            dgvExceptions.DataSource = exc;
            ApplyUnifiedLayout(dgvExceptions, "chkSelectExc");
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

        // =======================
        // Impression
        // =======================

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
                    using (PrintDocument pd = new PrintDocument())
                    using (PrintDialog diag = new PrintDialog())
                    {
                        pd.DefaultPageSettings.Landscape = true;

                        var mgr = new PrintManagerFailed(group.ToList());
                        pd.PrintPage += mgr.PrintDocument_PrintPage;

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
                    using (PrintDocument pd = new PrintDocument())
                    using (PrintDialog diag = new PrintDialog())
                    {
                        pd.DefaultPageSettings.Landscape = false;

                        var mgr = new PrintManager(group.ToList());
                        pd.PrintPage += mgr.PrintDocument_PrintPage;

                        diag.Document = pd;
                        if (diag.ShowDialog() == DialogResult.OK) pd.Print();
                    }
                }
            }
        }

        private void PrintExceptions(List<Transaction> txList)
        {
            if (txList == null || txList.Count == 0) return;

            using (PrintDocument pd = new PrintDocument())
            using (PrintDialog diag = new PrintDialog())
            {
                pd.DefaultPageSettings.Landscape = true;

                var mgr = new PrintManagerException(txList);
                pd.PrintPage += mgr.PrintDocument_PrintPage;

                diag.Document = pd;
                if (diag.ShowDialog() == DialogResult.OK) pd.Print();
            }
        }

        // =======================
        // Mise en forme grilles
        // =======================

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

        // Ordre unifié souhaité pour tous les onglets
        private static readonly string[] UnifiedOrder = {
            "TransactionDateTime",
            "TransactionID",
            "TransactionType",
            "ClientReferenceNumber",
            "FullName",
            "CreditAmount",
            "LastModified",
            "TransactionStatus",
            "TransactionFailureReason",
            "TransactionErrorCode",
            "Notes",
            "TransactionFlag"
        };

        private static readonly System.Collections.Generic.Dictionary<string, string> UnifiedHeaders
            = new System.Collections.Generic.Dictionary<string, string>
            {
                ["TransactionDateTime"] = "Transmis le",
                ["TransactionID"] = "TransactionID",
                ["TransactionType"] = "Type",
                ["ClientReferenceNumber"] = "# Client",
                ["FullName"] = "Nom",
                ["CreditAmount"] = "Montant",
                ["LastModified"] = "Dernière modif",
                ["TransactionStatus"] = "Statut",
                ["TransactionFailureReason"] = "Raison échec",
                ["TransactionErrorCode"] = "Code erreur",
                ["Notes"] = "Notes",
                ["TransactionFlag"] = "Drapeau"
            };

        private void ApplyUnifiedLayout(DataGridView dgv, string checkboxColumnName)
        {
            BaseGridTuning(dgv);

            // 1) Rendre visibles + renommer les colonnes d'intérêt
            foreach (var colName in UnifiedOrder)
            {
                if (dgv.Columns.Contains(colName))
                {
                    dgv.Columns[colName].Visible = true;
                    if (UnifiedHeaders.TryGetValue(colName, out var header))
                        dgv.Columns[colName].HeaderText = header;
                }
            }

            // 2) Case à cocher en première colonne si présente
            int startIndex = 0;
            if (!string.IsNullOrEmpty(checkboxColumnName) && dgv.Columns.Contains(checkboxColumnName))
            {
                dgv.Columns[checkboxColumnName].DisplayIndex = 0;
                startIndex = 1;
            }

            // 3) Appliquer l'ordre demandé
            for (int i = 0; i < UnifiedOrder.Length; i++)
            {
                string col = UnifiedOrder[i];
                if (dgv.Columns.Contains(col))
                    dgv.Columns[col].DisplayIndex = startIndex + i;
            }

            // 4) Masquer toutes les colonnes non listées
            var allowed = new System.Collections.Generic.HashSet<string>(UnifiedOrder);
            if (!string.IsNullOrEmpty(checkboxColumnName))
                allowed.Add(checkboxColumnName);

            foreach (DataGridViewColumn c in dgv.Columns)
            {
                if (!allowed.Contains(c.Name))
                    c.Visible = false;
            }

            // 5) Ajustements visuels
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            dgv.RowHeadersVisible = false;
            if (dgv.Columns.Contains("CreditAmount"))
                dgv.Columns["CreditAmount"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (dgv.Columns.Contains("TransactionDateTime"))
                dgv.Columns["TransactionDateTime"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            if (dgv.Columns.Contains("LastModified"))
                dgv.Columns["LastModified"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        private void BaseGridTuning(DataGridView dgv)
        {
            dgv.AllowUserToOrderColumns = false;
            dgv.AllowUserToResizeColumns = true;
            dgv.AutoGenerateColumns = true; // génération depuis List<Transaction>
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
                        var psi = new ProcessStartInfo { FileName = dir, UseShellExecute = true };
                        Process.Start(psi);
                    }
                }
                catch { /* no-op */ }
            }
        }
    }
}

