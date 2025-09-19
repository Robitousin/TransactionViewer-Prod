using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.Diagnostics;    // pour Process.Start / WaitForInputIdle
using System.IO;             // pour Path.GetDirectoryName
using TransactionViewer.DataAccess;
using TransactionViewer.Models;
using TransactionViewer.Printing;
using TransactionViewer.Services; // CsvExporter + ArchiveService (si présents dans ton projet)

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

                // Si application Win32 : attendre qu’elle soit prête (évite d’imprimer “trop tôt”)
                try { proc.WaitForInputIdle(timeoutMs); } catch { /* no-op */ }

                // Petite marge pour l’affichage visuel
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
                // Valider la dernière case cochée avant lecture
                dgvPrelevements.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvPrelevements.EndEdit();

                var selectedTx = GetCheckedTransactions(dgvPrelevements, "chkSelectPrelev");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour Prélèvements.");
                    return;
                }

                // 1) INSCRIPTION d’abord (comme le bouton Enregistrement)
                foreach (var tx in selectedTx)
                {
                    TransactionRepository.UpdatePrelevementDone(tx.TransactionID);
                }

                // 2) RAFRAÎCHIR l’UI tout de suite (comportement d’origine)
                RafraichirOnglets();
                Application.DoEvents(); // forcer le repaint avant impression

                // 3) IMPRIMER après que l’UI soit peinte
                this.BeginInvoke((Action)(() =>
                {
                    // Imprimer (prélèvements => isFailed=false => portrait)
                    PrintByDate(selectedTx, isFailed: false);
                }));

                lastPrintedList = selectedTx;
                lastPrintedIsFailed = false;
            }
            else if (currentTab == tabNSF)
            {
                // Valider la dernière case cochée avant lecture
                dgvNSF.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvNSF.EndEdit();

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

                // 1) INSCRIPTION d’abord (comme le bouton Enregistrement)
                foreach (var tx in selectedTx)
                {
                    TransactionRepository.UpdateNSFDone(tx.TransactionID);
                }

                // 2) RAFRAÎCHIR l’UI immédiatement
                RafraichirOnglets();
                Application.DoEvents(); // laisse la grille se mettre à jour visuellement

                // *** NOUVEAU : Lancer le programme tiers AVANT export/archivage et AVANT impression
                var creditOk = LancerProgrammeCreditEtAttendre(4000);
                // Si tu veux bloquer la suite si non lancé : if (!creditOk) return;

                // 3) EXPORT + ARCHIVAGE (format verrouillé) + OUVERTURE DOSSIER
                try
                {
                    string csvPath = CsvExporter.ExportTransactionsToCsvLockedFormat(
                        selectedTx,
                        destinationFilePath: null,
                        dateFormat: "yyyy-MM-dd"   // format verrouillé
                    );

                    string archivedPath = ArchiveService.MoveToNsfArchive(csvPath);

                    // Ouvrir automatiquement le dossier final et sélectionner le fichier
                    OuvrirExplorerSurFichier(archivedPath);
                }
                catch (Exception ex)
                {
                    // On signale l’erreur mais on n’empêche pas l’impression
                    MessageBox.Show("Erreur export/archivage NSF : " + ex.Message,
                        "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                // 4) IMPRIMER après que l’UI soit peinte
                this.BeginInvoke((Action)(() =>
                {
                    // Imprimer (NSF => isFailed=true => paysage)
                    PrintByDate(selectedTx, isFailed: true);
                }));

                lastPrintedList = selectedTx;
                lastPrintedIsFailed = true;
            }
            else if (currentTab == tabExceptions)
            {
                // Valider la dernière case cochée avant lecture
                dgvExceptions.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvExceptions.EndEdit();

                var selectedTx = GetCheckedTransactions(dgvExceptions, "chkSelectExc");
                if (!selectedTx.Any())
                {
                    MessageBox.Show("Aucune transaction cochée pour Exceptions.");
                    return;
                }

                // 1) Mettre IsVerifier = true, IsException = false (inscription)
                foreach (var tx in selectedTx)
                {
                    tx.IsVerifier = true;
                    tx.IsException = false;
                    TransactionRepository.InsertOrUpdateTransaction(tx);
                }

                // 2) RAFRAÎCHIR l’UI avant impression (même UX que l’autre bouton)
                RafraichirOnglets();
                Application.DoEvents();

                // 3) Imprimer via le manager d'Exceptions après repaint
                this.BeginInvoke((Action)(() =>
                {
                    PrintExceptions(selectedTx);
                }));
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
                dgvPrelevements.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvPrelevements.EndEdit();

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
                {
                    TransactionRepository.UpdateNSFDone(tx.TransactionID);
                }
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
                // => NSF, on imprime en PAYSAGE (groupé par LastModified.Date)
                var grouped = txList
                    .Where(t => DateTime.TryParse(t.LastModified, out var _))
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
                // => Prélèvements => PORTRAIT (groupé par TransactionDateTime.Date)
                var grouped = txList
                    .Where(t => DateTime.TryParse(t.TransactionDateTime, out var _))
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

        // Impression pour Exceptions via un manager dédié
        private void PrintExceptions(List<Transaction> txList)
        {
            if (txList == null || txList.Count == 0) return;

            PrintDocument pd = new PrintDocument();
            pd.DefaultPageSettings.Landscape = true; // ajustable selon ton besoin

            var mgr = new PrintManagerException(txList);
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

        // =====================================
        // = Helper : ouvrir le dossier de sortie et sélectionner le fichier
        // =====================================
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
