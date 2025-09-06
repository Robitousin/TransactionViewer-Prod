using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using TransactionViewer.Models;

namespace TransactionViewer.Printing
{
    public class PrintManagerException : IPrintManager
    {
        private List<Transaction> transactions;
        private int recordIndex = 0;
        private int pageCounter = 1;

        private readonly int[] columnWidths = { 90, 280, 90, 110, 90, 60, 220 };
        private readonly string[] headers =
        {
            "# Client",
            "Nom du Client",
            "Montant",
            "Transmis Le",
            "Date Exception",
            "Code",
            "Raison"
        };

        private int firstPageHeaderSpacing = 40;
        private int firstPageFooterSpacing = 40;
        private int generalHeaderSpacing = 20;
        private int generalFooterSpacing = 20;

        public PrintManagerException(List<Transaction> transactions)
        {
            this.transactions = transactions ?? new List<Transaction>();
        }

        public void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            // Forcer le paysage
            if (sender is PrintDocument doc)
            {
                doc.DefaultPageSettings.Landscape = true;
            }

            int lineHeight = 18;
            Font headerFont = new Font("Arial", 10, FontStyle.Bold);
            Font titleFont = new Font("Arial", 14, FontStyle.Bold);
            Font contentFont = new Font("Arial", 9);
            Font footerFont = new Font("Arial", 8);

            StringFormat leftAlign = new StringFormat { Alignment = StringAlignment.Near };
            StringFormat centerAlign = new StringFormat { Alignment = StringAlignment.Center };
            StringFormat rightAlign = new StringFormat { Alignment = StringAlignment.Far };

            int[] columnPositions = new int[columnWidths.Length];
            columnPositions[0] = 70;
            for (int i = 1; i < columnWidths.Length; i++)
            {
                columnPositions[i] = columnPositions[i - 1] + columnWidths[i - 1];
            }

            int topMargin = recordIndex == 0 ? 120 + firstPageHeaderSpacing : 90 + generalHeaderSpacing;
            int itemsPerPage = (e.MarginBounds.Height - topMargin -
                                (recordIndex == 0 ? firstPageFooterSpacing : generalFooterSpacing))
                                / lineHeight;

            // Titre
            e.Graphics.DrawString("Rapport de Transactions", titleFont, Brushes.Black,
                e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 70, centerAlign);

            e.Graphics.DrawString("Exceptions", titleFont, Brushes.Black,
                e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 45, centerAlign);

            e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, e.MarginBounds.Top - 10,
                                e.MarginBounds.Right, e.MarginBounds.Top - 10);

            // Première page
            if (recordIndex == 0)
            {
                if (System.IO.File.Exists("Resources/logo.png"))
                {
                    Image logo = Image.FromFile("Resources/logo.png");
                    e.Graphics.DrawImage(logo, new Rectangle(30, 22, 120, 86));
                }

                int dynamicTop = 130;
                e.Graphics.DrawString($"Nom : {GetDynamicName()}", contentFont, Brushes.Black, 30, dynamicTop, leftAlign);
                dynamicTop += lineHeight + 5;

                // Type => TransactionType
                string transactionType = "";
                if (transactions.Count > 0)
                {
                    transactionType = transactions[0].TransactionType ?? "";
                }
                e.Graphics.DrawString($"Type: {transactionType}", contentFont, Brushes.Black,
                                      30, dynamicTop, leftAlign);
                dynamicTop += lineHeight + 5;

                // Total
                e.Graphics.DrawString($"Total : {CalculateTotalWithLogging()}", contentFont, Brushes.Black,
                                      30, dynamicTop, leftAlign);

                // Référence => date "LastModified" du premier
                string refDate = "";
                if (transactions.Count > 0)
                {
                    var firstTx = transactions[0];
                    DateTime? dt = ParseDateTime(firstTx.LastModified);
                    refDate = dt.HasValue ? dt.Value.ToString("dd-MM-yyyy") : "";
                }
                e.Graphics.DrawString($"Référence : {refDate}", contentFont, Brushes.Black,
                    e.MarginBounds.Right - 10, 30, rightAlign);

                topMargin = dynamicTop + 40;
            }

            // Entêtes de colonnes
            for (int i = 0; i < headers.Length; i++)
            {
                var format = leftAlign;
                if (headers[i] == "Transmis Le" || headers[i] == "Date Exception")
                    format = centerAlign;

                e.Graphics.DrawString(headers[i], headerFont, Brushes.Black,
                    new RectangleF(columnPositions[i], topMargin, columnWidths[i], lineHeight), format);
            }
            topMargin += lineHeight;

            // Lignes
            while (recordIndex < transactions.Count && itemsPerPage > 0)
            {
                var tx = transactions[recordIndex];

                decimal amountDec = ParseDecimal(tx.CreditAmount);
                DateTime? dtTrans = ParseDateTime(tx.TransactionDateTime);
                DateTime? dtNsf = ParseDateTime(tx.LastModified);

                string clientRef = tx.ClientReferenceNumber ?? "";
                string fullName = tx.FullName ?? "";
                string amountStr = FormatCurrency(amountDec);
                string dateTransmis = FormatDate(dtTrans);
                string dateNsfStr = FormatDate(dtNsf);
                string code = tx.TransactionErrorCode ?? "";
                string reason = tx.TransactionFailureReason ?? "";

                string[] rowData =
                {
                    clientRef,
                    fullName,
                    amountStr,
                    dateTransmis,
                    dateNsfStr,
                    code,
                    reason
                };

                for (int i = 0; i < rowData.Length; i++)
                {
                    var format = leftAlign;
                    if (i == 3 || i == 4) format = centerAlign;

                    e.Graphics.DrawString(rowData[i], contentFont, Brushes.Black,
                        new RectangleF(columnPositions[i], topMargin, columnWidths[i], lineHeight), format);
                }

                topMargin += lineHeight;
                recordIndex++;
                itemsPerPage--;
            }

            // Pied de page
            int footerPosition = e.MarginBounds.Bottom -
                                 (recordIndex == 0 ? firstPageFooterSpacing : generalFooterSpacing);

            e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, footerPosition - 10,
                                e.MarginBounds.Right, footerPosition - 10);

            e.Graphics.DrawString($"Page {pageCounter}", footerFont, Brushes.Black,
                                  e.MarginBounds.Right - 50, footerPosition - 5, rightAlign);
            e.Graphics.DrawString($"Date : {DateTime.Now:dd-MM-yyyy}", footerFont, Brushes.Black,
                                  e.MarginBounds.Left + 10, footerPosition - 5, leftAlign);

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

        private string FormatCurrency(decimal val)
        {
            var ci = new CultureInfo("fr-CA");
            return val.ToString("N2", ci) + " $";
        }

        private string FormatDate(DateTime? dt)
        {
            if (dt.HasValue)
                return dt.Value.ToString("dd-MM-yyyy");
            return "";
        }

        private string CalculateTotalWithLogging()
        {
            decimal total = 0;
            foreach (var t in transactions)
            {
                total += ParseDecimal(t.CreditAmount);
            }
            return FormatCurrency(total);
        }

        private decimal ParseDecimal(string input)
        {
            if (string.IsNullOrEmpty(input)) return 0;
            var ci = new CultureInfo("fr-CA");
            if (decimal.TryParse(input, NumberStyles.Any, ci, out decimal val))
                return val;
            return 0;
        }

        private DateTime? ParseDateTime(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            var ci = CultureInfo.InvariantCulture;
            if (DateTime.TryParse(input, ci, DateTimeStyles.None, out DateTime dt))
                return dt;
            return null;
        }
    }
}


