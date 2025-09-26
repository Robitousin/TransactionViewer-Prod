using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using TransactionViewer.Models;

namespace TransactionViewer
{
    public class PrintManager : IPrintManager
    {
        private List<Transaction> transactions;
        private int recordIndex = 0;
        private int pageCounter = 1;

        private readonly int[] columnWidths = { 90, 280, 90, 110, 120 };
        private readonly string[] headers = { "# Client", "Nom du client", "Montant", "Transmis Le", "TransactionID" };
        private readonly string logoPath = @"Resources\logo.png";

        // Paramètres d'espacement
        private int firstPageHeaderSpacing = 40;
        private int firstPageFooterSpacing = 40;
        private int generalHeaderSpacing = 20;
        private int generalFooterSpacing = 20;

        public PrintManager(List<Transaction> transactions)
        {
            this.transactions = transactions ?? new List<Transaction>();
            Console.WriteLine("PrintManager initialized with transactions count: " + this.transactions.Count);
        }

        public void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            int lineHeight = 18;
            Font headerFont = new Font("Arial", 10, FontStyle.Bold);
            Font titleFont = new Font("Arial", 15, FontStyle.Bold);
            Font contentFont = new Font("Arial", 9);
            Font footerFont = new Font("Arial", 8);

            StringFormat leftAlign = new StringFormat { Alignment = StringAlignment.Near };
            StringFormat centerAlign = new StringFormat { Alignment = StringAlignment.Center };
            StringFormat rightAlign = new StringFormat { Alignment = StringAlignment.Far };

            // Position des colonnes
            int[] columnPositions = new int[columnWidths.Length];
            columnPositions[0] = 70;
            for (int i = 1; i < columnWidths.Length; i++)
            {
                columnPositions[i] = columnPositions[i - 1] + columnWidths[i - 1];
            }

            // Calcul de l'espace dispo
            int topMargin = recordIndex == 0 ? 130 + firstPageHeaderSpacing : 100 + generalHeaderSpacing;
            int itemsPerPage = Math.Max(1,
                (e.MarginBounds.Height - topMargin -
                 (recordIndex == 0 ? firstPageFooterSpacing : generalFooterSpacing))
                / lineHeight);

            // ===== Entête commun =====
            e.Graphics.DrawString("Rapport de Transactions", titleFont, Brushes.Black,
                 e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 70, centerAlign);

            e.Graphics.DrawString("Prélèvements", titleFont, Brushes.Black,
                e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 45, centerAlign);

            e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, e.MarginBounds.Top - 10,
                                e.MarginBounds.Right, e.MarginBounds.Top - 10);

            // Première page : logo, type, total, référence
            if (recordIndex == 0)
            {
                // Logo
                if (System.IO.File.Exists(logoPath))
                {
                    Image logo = Image.FromFile(logoPath);
                    e.Graphics.DrawImage(logo, new Rectangle(30, 22, 120, 86));
                }

                int dynamicTop = 130;
                e.Graphics.DrawString($"Nom: {GetDynamicName()}", contentFont, Brushes.Black, 30, dynamicTop, leftAlign);
                dynamicTop += lineHeight + 5;

                // (1) Afficher "Type: ..." à partir du TransactionType
                string transactionType = "";
                if (transactions.Count > 0)
                    transactionType = transactions[0].TransactionType ?? "";
                e.Graphics.DrawString($"Type: {transactionType}", contentFont, Brushes.Black, 30, dynamicTop, leftAlign);
                dynamicTop += lineHeight + 5;

                // (2) Afficher "Total: ..." -> addition CreditAmount
                e.Graphics.DrawString($"Total: {CalculateTotalWithLogging()}", contentFont, Brushes.Black,
                                      30, dynamicTop, leftAlign);

                // (3) Afficher "Référence : " + la date "TransactionDateTime" du premier
                string refDate = "";
                if (transactions.Count > 0)
                {
                    var firstTx = transactions[0];
                    DateTime? dt = ParseDateTime(firstTx.TransactionDateTime);
                    refDate = dt.HasValue ? dt.Value.ToString("dd-MM-yyyy") : "";
                }
                e.Graphics.DrawString($"Référence : {refDate}", contentFont, Brushes.Black,
                                      e.MarginBounds.Right - 10, 30, rightAlign);

                topMargin = dynamicTop + 40;
            }

            // ===== En-têtes de colonnes =====
            for (int i = 0; i < headers.Length; i++)
            {
                var format = (headers[i] == "Transmis Le" || headers[i] == "TransactionID")
                    ? centerAlign
                    : leftAlign;

                e.Graphics.DrawString(headers[i], headerFont, Brushes.Black,
                    new RectangleF(columnPositions[i], topMargin, columnWidths[i], lineHeight), format);
            }
            topMargin += lineHeight;

            // ===== Lignes =====
            while (recordIndex < transactions.Count && itemsPerPage > 0)
            {
                var tx = transactions[recordIndex];

                decimal creditDecimal = ParseDecimal(tx.CreditAmount);
                DateTime? dateParsed = ParseDateTime(tx.TransactionDateTime);

                string clientRef = ClientRefOrAccountIdNumeric(tx); // <-- règle appliquée ici
                string fullName = tx.FullName ?? "";
                string amountStr = FormatCurrency(creditDecimal);
                string dateStr = FormatDate(dateParsed);
                string txId = tx.TransactionID ?? "";

                string[] rowData = { clientRef, fullName, amountStr, dateStr, txId };

                for (int i = 0; i < rowData.Length; i++)
                {
                    var format = (i == 3 || i == 4) ? centerAlign : leftAlign;
                    e.Graphics.DrawString(rowData[i], contentFont, Brushes.Black,
                        new RectangleF(columnPositions[i], topMargin, columnWidths[i], lineHeight), format);
                }

                topMargin += lineHeight;
                recordIndex++;
                itemsPerPage--;
            }

            // ===== Pied de page =====
            int footerPosition = e.MarginBounds.Bottom -
                                 (recordIndex == 0 ? firstPageFooterSpacing : generalFooterSpacing);

            e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, footerPosition - 10,
                                e.MarginBounds.Right, footerPosition - 10);

            e.Graphics.DrawString($"Page {pageCounter}", footerFont, Brushes.Black,
                                  e.MarginBounds.Right - 50, footerPosition - 5, rightAlign);
            e.Graphics.DrawString($"Date : {DateTime.Now:dd/MM/yyyy}", footerFont, Brushes.Black,
                                  e.MarginBounds.Left + 10, footerPosition - 5, leftAlign);

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

        // ================== Méthodes Utilitaires ==================

        private string GetDynamicName()
        {
            return "8341855 Canada Inc";
        }

        /// <summary> Parse ex. "144,00" en 144.00 decimal (fr-CA) </summary>
        private decimal ParseDecimal(string input)
        {
            if (string.IsNullOrEmpty(input)) return 0;
            CultureInfo ci = new CultureInfo("fr-CA");
            if (decimal.TryParse(input, NumberStyles.Any, ci, out decimal val))
                return val;
            return 0;
        }

        /// <summary> Parse ex. "2024-12-13 10:05:46" en DateTime? </summary>
        private DateTime? ParseDateTime(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            CultureInfo ci = CultureInfo.InvariantCulture;
            if (DateTime.TryParse(input, ci, DateTimeStyles.None, out DateTime dt))
                return dt;
            return null;
        }

        /// <summary> Format en "144,00 $" (fr-CA) </summary>
        private string FormatCurrency(decimal val)
        {
            CultureInfo ci = new CultureInfo("fr-CA");
            return val.ToString("N2", ci) + " $";
        }

        /// <summary> Format la date en "dd-MM-yyyy" </summary>
        private string FormatDate(DateTime? dt)
        {
            if (dt.HasValue)
                return dt.Value.ToString("dd-MM-yyyy");
            return "";
        }

        /// <summary> Calcule le total (en decimal) sur toutes les transactions </summary>
        private string CalculateTotalWithLogging()
        {
            decimal total = 0;
            foreach (var t in transactions)
            {
                total += ParseDecimal(t.CreditAmount);
            }
            Console.WriteLine($"Total calculated: {total}");
            return FormatCurrency(total);
        }

        // ====== Règle #Client : ClientReferenceNumber sinon ClientAccountID (numérique) ======
        private static bool IsDigitsOnly(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            foreach (char c in s.Trim())
                if (!char.IsDigit(c)) return false;
            return true;
        }

        private static string ClientRefOrAccountIdNumeric(Transaction tx)
        {
            var refNum = (tx.ClientReferenceNumber ?? "").Trim();
            if (!string.IsNullOrEmpty(refNum)) return refNum;

            var accountId = (tx.ClientAccountID ?? "").Trim();
            if (IsDigitsOnly(accountId)) return accountId;

            return "";
        }
    }
}





