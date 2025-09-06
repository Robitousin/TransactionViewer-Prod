using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using TransactionViewer.Models;

namespace TransactionViewer
{
    public class PrintManagerFailed : IPrintManager
    {
        // On stocke la liste triée par Montant (CreditAmount)
        private List<Transaction> transactions;

        // Indices pour le parcours d'impression
        private int recordIndex = 0;
        private int pageCounter = 1;

        // Nouvelles largeurs et ordre de colonnes
        // #Client, Nom, Montant, DateNSF, TransmisLe, Code, NSF Raison
        private readonly int[] columnWidths = { 90, 280, 90, 110, 110, 60, 220 };
        private readonly string[] headers =
        {
            "# Client",
            "Nom du Client",
            "Montant",
            "Date NSF",
            "Transmis Le",
            "Code",
            "NSF Raison"
        };

        // Paramètres d'espacement
        private int firstPageHeaderSpacing = 40;
        private int firstPageFooterSpacing = 40;
        private int generalHeaderSpacing = 20;
        private int generalFooterSpacing = 20;

        /// <summary>
        /// Constructeur : on trie la liste du plus petit au plus grand Montant
        /// </summary>
        public PrintManagerFailed(List<Transaction> transactions)
        {
            // Tri par montant ascendant
            this.transactions = (transactions ?? new List<Transaction>())
                .OrderBy(tx => ParseDecimal(tx.CreditAmount))
                .ToList();
        }

        public void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            int lineHeight = 18;

            Font headerFont = new Font("Arial", 10, FontStyle.Bold);
            Font titleFont = new Font("Arial", 14, FontStyle.Bold);
            Font contentFont = new Font("Arial", 9);
            Font footerFont = new Font("Arial", 8);

            StringFormat leftAlign = new StringFormat { Alignment = StringAlignment.Near };
            StringFormat centerAlign = new StringFormat { Alignment = StringAlignment.Center };
            StringFormat rightAlign = new StringFormat { Alignment = StringAlignment.Far };

            // Positions des colonnes
            int[] columnPositions = new int[columnWidths.Length];
            columnPositions[0] = 70;
            for (int i = 1; i < columnWidths.Length; i++)
            {
                columnPositions[i] = columnPositions[i - 1] + columnWidths[i - 1];
            }

            // Calcul des marges (haut, espace dispo, etc.)
            int topMargin = recordIndex == 0
                ? 120 + firstPageHeaderSpacing
                : 90 + generalHeaderSpacing;

            int itemsPerPage = (e.MarginBounds.Height - topMargin -
                               (recordIndex == 0
                                   ? firstPageFooterSpacing
                                   : generalFooterSpacing))
                               / lineHeight;

            // ====== En-tête ======
            e.Graphics.DrawString("Rapport de Transactions", titleFont, Brushes.Black,
                e.MarginBounds.Left + (e.MarginBounds.Width / 2),
                e.MarginBounds.Top - 70,
                centerAlign);

            e.Graphics.DrawString("NSF", titleFont, Brushes.Black,
                e.MarginBounds.Left + (e.MarginBounds.Width / 2),
                e.MarginBounds.Top - 45,
                centerAlign);

            e.Graphics.DrawLine(Pens.Black,
                e.MarginBounds.Left,
                e.MarginBounds.Top - 10,
                e.MarginBounds.Right,
                e.MarginBounds.Top - 10);

            // Champs dynamiques sur la première page
            if (recordIndex == 0)
            {
                // Logo
                if (System.IO.File.Exists("Resources/logo.png"))
                {
                    Image logo = Image.FromFile("Resources/logo.png");
                    e.Graphics.DrawImage(logo, new Rectangle(30, 22, 120, 86));
                }

                int dynamicTop = 130;
                e.Graphics.DrawString($"Nom : {GetDynamicName()}", contentFont, Brushes.Black,
                    30, dynamicTop, leftAlign);
                dynamicTop += lineHeight + 5;

                // Type => TransactionType (s'il y a au moins une transaction)
                string transactionType = "";
                if (transactions.Count > 0)
                {
                    transactionType = transactions[0].TransactionType ?? "";
                }
                e.Graphics.DrawString($"Type: {transactionType}", contentFont, Brushes.Black,
                    30, dynamicTop, leftAlign);
                dynamicTop += lineHeight + 5;

                // Total => somme de CreditAmount
                e.Graphics.DrawString($"Total : {CalculateTotalWithLogging()}", contentFont, Brushes.Black,
                    30, dynamicTop, leftAlign);

                // Référence => date "LastModified" du premier
                string refDate = "";
                if (transactions.Count > 0)
                {
                    var firstTx = transactions[0];
                    DateTime? dt = ParseDateTime(firstTx.LastModified);
                    refDate = dt.HasValue
                        ? dt.Value.ToString("dd/MM/yyyy") // Format jj/MM/aaaa
                        : "";
                }
                e.Graphics.DrawString($"Référence : {refDate}", contentFont, Brushes.Black,
                    e.MarginBounds.Right - 10, 30, rightAlign);

                topMargin = dynamicTop + 40;
            }

            // ====== En-têtes de colonnes ======
            for (int i = 0; i < headers.Length; i++)
            {
                // On centre "Date NSF" et "Transmis Le" si on veut
                var format = leftAlign;
                if (headers[i] == "Date NSF" || headers[i] == "Transmis Le")
                    format = centerAlign;

                e.Graphics.DrawString(headers[i], headerFont, Brushes.Black,
                    new RectangleF(columnPositions[i], topMargin, columnWidths[i], lineHeight),
                    format);
            }
            topMargin += lineHeight;

            // ====== Lignes de données ======
            while (recordIndex < transactions.Count && itemsPerPage > 0)
            {
                var tx = transactions[recordIndex];

                // Montant => on l'affiche en ordre trié
                decimal amountDec = ParseDecimal(tx.CreditAmount);

                // Date NSF => LastModified
                DateTime? dtNsf = ParseDateTime(tx.LastModified);
                // Transmis Le => TransactionDateTime
                DateTime? dtTrans = ParseDateTime(tx.TransactionDateTime);

                string clientRef = tx.ClientReferenceNumber ?? "";
                string fullName = tx.FullName ?? "";
                string amountStr = FormatCurrency(amountDec);

                // "Date NSF"
                string dateNsfStr = FormatDate(dtNsf);
                // "Transmis Le"
                string dateTransStr = FormatDate(dtTrans);

                // Code => TransactionErrorCode
                string code = tx.TransactionErrorCode ?? "";
                // NSF Raison => TransactionFailureReason
                string reason = tx.TransactionFailureReason ?? "";

                // Ordre: #Client, Nom, Montant, DateNSF, TransmisLe, Code, NSF Raison
                string[] rowData =
                {
                    clientRef,
                    fullName,
                    amountStr,
                    dateNsfStr,
                    dateTransStr,
                    code,
                    reason
                };

                for (int i = 0; i < rowData.Length; i++)
                {
                    var format = leftAlign;
                    // On centre pour les dates
                    if (i == 3 || i == 4) format = centerAlign;

                    e.Graphics.DrawString(rowData[i], contentFont, Brushes.Black,
                        new RectangleF(columnPositions[i], topMargin, columnWidths[i], lineHeight),
                        format);
                }

                topMargin += lineHeight;
                recordIndex++;
                itemsPerPage--;
            }

            // ====== Pied de page ======
            int footerPosition = e.MarginBounds.Bottom -
                (recordIndex == 0 ? firstPageFooterSpacing : generalFooterSpacing);

            e.Graphics.DrawLine(Pens.Black,
                e.MarginBounds.Left,
                footerPosition - 10,
                e.MarginBounds.Right,
                footerPosition - 10);

            // Page X
            e.Graphics.DrawString($"Page {pageCounter}", footerFont, Brushes.Black,
                e.MarginBounds.Right - 50, footerPosition - 5, rightAlign);

            // Date => Format jj/MM/aaaa
            e.Graphics.DrawString($"Date : {DateTime.Now:dd/MM/yyyy}", footerFont, Brushes.Black,
                e.MarginBounds.Left + 10,
                footerPosition - 5,
                leftAlign);

            // Pagination
            if (recordIndex < transactions.Count)
            {
                e.HasMorePages = true;
                pageCounter++;
            }
            else
            {
                e.HasMorePages = false;
                pageCounter = 1;
            }
        }

        // ================== Méthodes utilitaires ==================

        private string GetDynamicName()
        {
            return "8341855 Canada Inc";
        }

        /// <summary>
        /// Additionne tous les CreditAmount de la liste triée
        /// </summary>
        private string CalculateTotalWithLogging()
        {
            decimal total = 0;
            foreach (var t in transactions)
            {
                total += ParseDecimal(t.CreditAmount);
            }
            return FormatCurrency(total);
        }

        /// <summary> Convertit un string Montant en decimal (fr-CA) </summary>
        private decimal ParseDecimal(string input)
        {
            if (string.IsNullOrEmpty(input)) return 0;
            var ci = new CultureInfo("fr-CA");
            if (decimal.TryParse(input, NumberStyles.Any, ci, out decimal val))
                return val;
            return 0;
        }

        /// <summary> Convertit un string Date ex. "2024-12-13 10:05:46" en DateTime? </summary>
        private DateTime? ParseDateTime(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            var ci = CultureInfo.InvariantCulture;
            if (DateTime.TryParse(input, ci, DateTimeStyles.None, out DateTime dt))
                return dt;
            return null;
        }

        /// <summary> Format un decimal => "144,00 $" (fr-CA) </summary>
        private string FormatCurrency(decimal val)
        {
            var ci = new CultureInfo("fr-CA");
            return val.ToString("N2", ci) + " $";
        }

        /// <summary> Format Date => "dd/MM/yyyy" </summary>
        private string FormatDate(DateTime? dt)
        {
            if (dt.HasValue)
                return dt.Value.ToString("dd/MM/yyyy");
            return "";
        }
    }
}








