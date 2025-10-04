// TransactionViewer - Form1.cs (nettoyé et largement commenté)
// Version: 0.0.22 (base), révision: nettoyage commentaires 2025-10-01
// Objectif: UI WinForms pour gérer Prélèvements, NSF et Exceptions
// - Import JSON → DB
// - Sélection, enregistrement, impression (portrait/paysage)
// - Export/archivage CSV NSF + ouverture de l'explorateur
// - Lancement d'un programme tiers (Credit.exe) avant l'impression NSF

using System;
using System.Collections.Generic;
using System.Configuration; // AppSettings (NsfOutputRoot, ArchiveRoot)
using System.Data;
using System.Diagnostics;   // Process.Start / WaitForInputIdle
using System.Drawing.Printing;
using System.IO;            // Path, Directory
using System.Linq;
using System.Windows.Forms;
using TransactionViewer.DataAccess;   // TransactionRepository
using System.Threading.Tasks;
// Ces deux espaces de noms sont utilisés dans ta version récente.
// Assure-toi que les classes existent : ClientRefHelper, CsvExporter, ArchiveService.
using TransactionViewer.Helpers;      // ClientRefHelper.GetClientRef(Transaction)
using TransactionViewer.Models;       // Transaction
using TransactionViewer.Printing;     // PrintManager, PrintManagerFailed, PrintManagerException
using TransactionViewer.Services;     // CsvExporter.ExportTransactionsToCsvLockedFormat, ArchiveService

namespace TransactionViewer
{
    /// <summary>
    /// Form principal : gère l'UI, la sélection des transactions et les actions (import, enregistrer, imprimer, exporter NSF).
    /// </summary>
    public partial class Form1 : Form
    {
        #region === Champs privés & constantes ===

        /// <summary>Dernière liste imprimée (utilisée pour "Dernier Print").</summary>
        private List<Transaction> lastPrintedList = null;

        /// <summary>Indique si la dernière impression était NSF (paysage) ou Prélèvement (portrait).</summary>
        private bool lastPrintedIsFailed = false;

        /// <summary>Chemin du programme tiers à lancer AVANT l'impression NSF.</summary>
        private const string CHEMIN_CREDIT_EXE = @"C:\v100\Credit.exe";

        /// <summary>Ordre unifié des colonnes à afficher dans les grilles.</summary>
        private static readonly string[] UnifiedOrder =
        {
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

        /// <summary>Libellés d'en-têtes par colonne.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> UnifiedHeaders =
            new System.Collections.Generic.Dictionary<string, string>
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
                ["TransactionFlag"] = "Drapeau",
            };

        #endregion

        #region === Ctor & Load ===

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Active un fallback d'affichage pour # Client (ClientReferenceNumber)
            // afin d'utiliser une logique centralisée (ClientRefHelper) si la valeur est vide.
            dgvPrelevements.CellFormatting += Dgv_CellFormatting_ClientRefFallback;
            dgvNSF.CellFormatting += Dgv_CellFormatting_ClientRefFallback;
            dgvExceptions.CellFormatting += Dgv_CellFormatting_ClientRefFallback;

            // Chargement initial des 3 onglets
            RafraichirOnglets();
        }

        #endregion

        #region === Import / Rafraîchir ===

        /// <summary>
        /// Importer un fichier JSON (Transactions) → DB puis rafraîchir l'UI.
        /// </summary>
        private void btnImporter_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Fichiers JSON|*.json";
                ofd.Title = "Sélectionner un fichier JSON";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    JsonImportService.ImportTransactionsFromFile(ofd.FileName);
                    RafraichirOnglets();
                }
            }
        }

        /// <summary>Recharger les 3 onglets depuis la DB.</summary>
        private void btnRafraichir_Click(object sender, EventArgs e)
        {
            RafraichirOnglets();
        }

        #endregion

        #region === Mise en forme des cellules (# Client fallback) ===

        /// <summary>
        /// Sur la colonne ClientReferenceNumber : applique un fallback affichable via ClientRefHelper.GetClientRef.
        /// </summary>
        private void Dgv_CellFormatting_ClientRefFallback(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var col = dgv.Columns[e.ColumnIndex];

            // Cible strictement la colonne liée à ClientReferenceNumber
            var isClientRefCol = (col.DataPropertyName ?? col.Name) == "ClientReferenceNumber";
            if (!isClientRefCol)
                return;

            // Récupère la Transaction liée à la ligne
            var tx = dgv.Rows[e.RowIndex].DataBoundItem as Transaction;
            if (tx == null)
                return;

            // Calcul du texte à afficher (# Client réel ou logique de fallback)
            var display = ClientRefHelper.GetClientRef(tx);
            if (display != null)
            {
                e.Value = display;
                e.FormattingApplied = true;
            }
        }

        #endregion
        private async void btnRecherche_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;

                // Détecter l’onglet actif (ne pas réutiliser les noms de TabPage comme bool!)
                bool isPrelevementsTab = (tabControl1.SelectedTab == tabPrelevements);
                bool isNSFTab = (tabControl1.SelectedTab == tabNSF);
                bool isExceptionsTab = (tabControl1.SelectedTab == tabExceptions);

                // Critères
                string kw = txtRecherche.Text?.Trim();
                DateTime? d1 = dtDu.Value.Date;
                DateTime? d2 = dtAu.Value.Date;
                string st = (cboStatut.SelectedItem as string) ?? "";

                // Exécuter la recherche hors UI
                var data = await Task.Run(() =>
                    TransactionRepository.SearchTransactions(
                        keyword: kw,
                        fromDate: d1,
                        toDate: d2,
                        status: st,
                        onlyFailedTab: isNSFTab,
                        onlyPrelevementTab: isPrelevementsTab,
                        onlyExceptionTab: isExceptionsTab,
                        skip: 0, take: 200
                    )
                );

                // Alimenter la grille de l’onglet actif
                if (isPrelevementsTab)
                {
                    dgvPrelevements.DataSource = data;
                    ApplyUnifiedLayout(dgvPrelevements, "chkSelectPrelev");
                }
                else if (isNSFTab)
                {
                    dgvNSF.DataSource = data;
                    ApplyUnifiedLayout(dgvNSF, "chkSelectNSF");
                }
                else if (isExceptionsTab)
                {
                    dgvExceptions.DataSource = data;
                    ApplyUnifiedLayout(dgvExceptions, "chkSelectExc");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur de recherche : " + ex.Message, "Recherche",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        #region === Bouton Print / Inscrire (par onglet) ===

        /// <summary>
        /// Enregistre et lance l'impression du contenu coché de l'onglet courant.
        /// - Prélèvements → portrait
        /// - NSF → paysage (+ lance Credit.exe, exporte & archive CSV puis ouvre l'explorateur)
        /// - Exceptions → bascule IsVerifier=true, IsException=false puis imprime
        /// </summary>
        private void btnImpressionEnregistrement_Click(object sender, EventArgs e)
        {
            var currentTab = tabControl1.SelectedTab;

            if (currentTab == tabPrelevements)
            {
                // Forcer la prise en compte des checkbox cochées
                dgvPrelevements.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvPrelevements.EndEdit();

                var selectedTx = GetCheckedTransactions(dgvPrelevements, "chkSelectPrelev");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour Prélèvements.");
                    return;
                }

                // Marquer IsPrelevementDone
                foreach (var tx in selectedTx)
                    TransactionRepository.UpdatePrelevementDone(tx.TransactionID);

                // Rafraîchir l'interface AVANT impression pour refléter l'état
                RafraichirOnglets();
                Application.DoEvents();

                // Impression portrait (asynchrone via BeginInvoke pour laisser respirer l'UI)
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

                // Marquer IsNSFDone
                foreach (var tx in selectedTx)
                    TransactionRepository.UpdateNSFDone(tx.TransactionID);

                RafraichirOnglets();
                Application.DoEvents();

                // 1) Lancer programme tiers (si souhaité)
                var creditOk = LancerProgrammeCreditEtAttendre(4000);
                // if (!creditOk) return; // ← décommente si tu veux bloquer la suite si Credit.exe échoue

                // 2) Export CSV (format "verrouillé"), archive et ouvrir l'explorateur
                try
                {
                    string nsfRoot = GetNsfRoot();
                    Directory.CreateDirectory(nsfRoot);

                    // Nom de fichier explicite (évite Null/empty)
                    var outFile = Path.Combine(nsfRoot, $"NSF_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                    string csvPath = CsvExporter.ExportTransactionsToCsvLockedFormat(
                        selectedTx,
                        destinationFilePath: outFile,
                        dateFormat: "yyyy-MM-dd"
                    );

                    string archiveRoot = GetArchiveRoot();
                    Directory.CreateDirectory(archiveRoot);

                    string archivedPath = ArchiveService.MoveToNsfArchive(csvPath, archiveRoot);
                    OuvrirExplorerSurFichier(archivedPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Erreur export/archivage NSF : " + ex.Message,
                        "Erreur",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                // 3) Impression paysage
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

                // Basculer l'état Exception → Vérifié
                foreach (var tx in selectedTx)
                {
                    tx.IsVerifier = true;
                    tx.IsException = false;
                    TransactionRepository.InsertOrUpdateTransaction(tx);
                }

                RafraichirOnglets();
                Application.DoEvents();

                // Impression Exceptions (paysage par défaut dans PrintManagerException)
                this.BeginInvoke((Action)(() =>
                {
                    PrintExceptions(selectedTx);
                }));
            }
        }

        #endregion

        #region === Bouton No Print / Inscrire ===

        /// <summary>
        /// Enregistrer les transactions cochées sans impression (respecte les règles de chaque onglet).
        /// </summary>
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

        #endregion

        #region === Bouton Dernier Print ===

        /// <summary>
        /// Relance l'impression sur la dernière liste imprimée (portrait/paysage conservé).
        /// </summary>
        private void btnReimprimerDerniere_Click(object sender, EventArgs e)
        {
            if (lastPrintedList == null || lastPrintedList.Count == 0)
            {
                MessageBox.Show("Aucune impression précédente.");
                return;
            }
            PrintByDate(lastPrintedList, lastPrintedIsFailed);
        }

        #endregion

        #region === Rafraîchissement des onglets ===

        /// <summary>
        /// Recharge les 3 grilles depuis la DB et applique la mise en page unifiée.
        /// </summary>
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

        #endregion

        #region === Sélection des lignes cochées ===

        /// <summary>
        /// Retourne la liste des transactions cochées dans une grille.
        /// </summary>
        private List<Transaction> GetCheckedTransactions(DataGridView dgv, string checkboxColumnName)
        {
            var list = new List<Transaction>();
            var allTx = dgv.DataSource as List<Transaction>;
            if (allTx == null)
                return list;

            foreach (DataGridViewRow row in dgv.Rows)
            {
                bool isChecked = false;
                if (row.Cells[checkboxColumnName].Value != null)
                    bool.TryParse(Convert.ToString(row.Cells[checkboxColumnName].Value), out isChecked);

                if (isChecked)
                {
                    int idx = row.Index;
                    if (idx >= 0 && idx < allTx.Count)
                        list.Add(allTx[idx]);
                }
            }
            return list;
        }

        #endregion

        #region === Impression ===

        /// <summary>
        /// Impression groupée par date :
        /// - NSF (isFailed=true) → groupement par LastModified, paysage
        /// - Prélèvements (isFailed=false) → groupement par TransactionDateTime, portrait
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
                // NSF : groupement par LastModified
                var grouped = txList
                    .Where(t => DateTime.TryParse(t.LastModified, out _))
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
                    using (PrintDialog dlg = new PrintDialog())
                    {
                        pd.DefaultPageSettings.Landscape = true;
                        var mgr = new PrintManagerFailed(group.ToList());
                        pd.PrintPage += mgr.PrintDocument_PrintPage;
                        dlg.Document = pd;
                        if (dlg.ShowDialog() == DialogResult.OK)
                            pd.Print();
                    }
                }
            }
            else
            {
                // Prélèvements : groupement par TransactionDateTime
                var grouped = txList
                    .Where(t => DateTime.TryParse(t.TransactionDateTime, out _))
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
                    using (PrintDialog dlg = new PrintDialog())
                    {
                        pd.DefaultPageSettings.Landscape = false;
                        var mgr = new PrintManager(group.ToList());
                        pd.PrintPage += mgr.PrintDocument_PrintPage;
                        dlg.Document = pd;
                        if (dlg.ShowDialog() == DialogResult.OK)
                            pd.Print();
                    }
                }
            }
        }

        /// <summary>
        /// Impression pour l'onglet Exceptions (forcée en paysage).
        /// </summary>
        private void PrintExceptions(List<Transaction> txList)
        {
            if (txList == null || txList.Count == 0)
                return;

            using (PrintDocument pd = new PrintDocument())
            using (PrintDialog dlg = new PrintDialog())
            {
                pd.DefaultPageSettings.Landscape = true;
                var mgr = new PrintManagerException(txList);
                pd.PrintPage += mgr.PrintDocument_PrintPage;
                dlg.Document = pd;
                if (dlg.ShowDialog() == DialogResult.OK)
                    pd.Print();
            }
        }

        #endregion

        #region === CheckBox "Cocher Tout" & tuning grilles ===

        private void chkSelectAllPrelev_CheckedChanged(object sender, EventArgs e)
        {
            SetAllRowsChecked(dgvPrelevements, chkSelectAllPrelev.Checked, "chkSelectPrelev");
        }

        private void chkSelectAllNSF_CheckedChanged(object sender, EventArgs e)
        {
            SetAllRowsChecked(dgvNSF, chkSelectAllNSF.Checked, "chkSelectNSF");
        }

        /// <summary>Cocher/Décocher toutes les lignes d'une grille.</summary>
        private void SetAllRowsChecked(DataGridView dgv, bool check, string checkboxColumnName)
        {
            dgv.EndEdit();
            foreach (DataGridViewRow row in dgv.Rows)
                row.Cells[checkboxColumnName].Value = check;
            dgv.EndEdit();
            dgv.Refresh();
        }

        /// <summary>
        /// Applique un layout standardisé à une grille (ordre, en-têtes, alignements, masquage colonnes hors scope).
        /// </summary>
        private void ApplyUnifiedLayout(DataGridView dgv, string checkboxColumnName)
        {
            BaseGridTuning(dgv);

            // 1) Rendre visibles & renommer les colonnes d'intérêt
            foreach (var colName in UnifiedOrder)
            {
                if (dgv.Columns.Contains(colName))
                {
                    dgv.Columns[colName].Visible = true;
                    if (UnifiedHeaders.TryGetValue(colName, out var header))
                        dgv.Columns[colName].HeaderText = header;
                }
            }

            // 2) Case à cocher en première colonne (si présente)
            int startIndex = 0;
            if (!string.IsNullOrEmpty(checkboxColumnName) && dgv.Columns.Contains(checkboxColumnName))
            {
                dgv.Columns[checkboxColumnName].DisplayIndex = 0;
                startIndex = 1;
            }

            // 3) Ordre unifié juste après la colonne de checkbox éventuelle
            for (int i = 0; i < UnifiedOrder.Length; i++)
            {
                string col = UnifiedOrder[i];
                if (dgv.Columns.Contains(col))
                    dgv.Columns[col].DisplayIndex = startIndex + i;
            }

            // 4) Masquer les colonnes non listées pour réduire le bruit visuel
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

        /// <summary>Réglages de base pour toutes les grilles.</summary>
        private void BaseGridTuning(DataGridView dgv)
        {
            dgv.AllowUserToOrderColumns = false; // l'ordre est imposé par ApplyUnifiedLayout
            dgv.AllowUserToResizeColumns = true;
            dgv.AutoGenerateColumns = true;  // génération automatique depuis List<Transaction>
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

        #endregion

        #region === Lancement programme tiers (NSF) ===

        /// <summary>
        /// Lance C:\v100\Credit.exe (si présent) et laisse le temps d'initialiser (WaitForInputIdle).
        /// Retourne true si le processus a été lancé correctement.
        /// </summary>
        private bool LancerProgrammeCreditEtAttendre(int timeoutMs = 4000)
        {
            try
            {
                if (!File.Exists(CHEMIN_CREDIT_EXE))
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
                    WorkingDirectory = Path.GetDirectoryName(CHEMIN_CREDIT_EXE),
                    // Verb = "runas", // décommente si l’appli exige des droits admin (UAC)
                };

                var proc = Process.Start(psi);
                if (proc == null)
                    return false;

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

        #endregion

        #region === Utilitaires (chemins, explorer) ===

        /// <summary>Récupère la racine NSF depuis appSettings (NsfOutputRoot) ou fallback Documents\TransactionViewer\NSF.</summary>
        private static string GetNsfRoot()
        {
            var nsfRoot = ConfigurationManager.AppSettings["NsfOutputRoot"];
            if (string.IsNullOrWhiteSpace(nsfRoot))
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                nsfRoot = Path.Combine(docs, "TransactionViewer", "NSF");
            }
            return nsfRoot;
        }

        /// <summary>Récupère la racine d'archive depuis appSettings (ArchiveRoot) ou fallback Documents\TransactionViewer\Archive.</summary>
        private static string GetArchiveRoot()
        {
            var archiveRoot = ConfigurationManager.AppSettings["ArchiveRoot"];
            if (string.IsNullOrWhiteSpace(archiveRoot))
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                archiveRoot = Path.Combine(docs, "TransactionViewer", "Archive");
            }
            return archiveRoot;
        }

        /// <summary>Ouvre l'explorateur Windows sur un fichier (sélectionné) ou à défaut sur son dossier.</summary>
        private void OuvrirExplorerSurFichier(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return;

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

        #endregion
    }
}