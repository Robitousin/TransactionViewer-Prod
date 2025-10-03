using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using TransactionViewer.Models;

namespace TransactionViewer.Printing
{
    /// <summary>
    /// Impression NSF (paysage) – avec cartouche 1ère page (Nom, Type, Total, Référence),
    /// ligne d’en-tête sous les titres, colonnes normalisées et pied de page harmonisé.
    /// </summary>
    public class PrintManagerFailed : IPrintManager
    {
        private readonly List<Transaction> transactions;
        private int recordIndex = 0;
        private int pageCounter = 1;

        private readonly int[] columnWidths = { 90, 280, 90, 110, 110, 60, 220 };
        private readonly string[] headers = { "# Client", "Nom du Client", "Montant", "Date NSF", "Transmis Le", "Code", "NSF Raison" };

        private readonly string logoPath = @"Resources\\logo.png";

        public PrintManagerFailed(List<Transaction> list)
        {
            // Tri montant ascendant
            transactions = (list ?? new List<Transaction>()).OrderBy(t => ParseDecimal(t.CreditAmount)).ToList();
        }

        public void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            if (sender is PrintDocument doc) doc.DefaultPageSettings.Landscape = true;

            int lineHeight = 18;
            Font headerFont = new Font("Arial", 10, FontStyle.Bold);
            Font titleFont = new Font("Arial", 14, FontStyle.Bold);
            Font contentFont = new Font("Arial", 9);
            Font footerFont = new Font("Arial", 8);

            StringFormat L = new StringFormat { Alignment = StringAlignment.Near };
            StringFormat C = new StringFormat { Alignment = StringAlignment.Center };
            StringFormat R = new StringFormat { Alignment = StringAlignment.Far };

            int[] colPos = new int[columnWidths.Length];
            colPos[0] = 70;
            for (int i = 1; i < columnWidths.Length; i++)
                colPos[i] = colPos[i - 1] + columnWidths[i - 1];

            // Titres
            e.Graphics.DrawString("Rapport de Transactions", titleFont, Brushes.Black,
                e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 70, C);
            e.Graphics.DrawString("NSF", titleFont, Brushes.Black,
                e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 45, C);

            // Ligne d’en-tête (sous les titres)
            int headerLineY = e.MarginBounds.Top - 10;
            e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, headerLineY, e.MarginBounds.Right, headerLineY);

            // Cartouche 1ère page
            int topMargin = 90;
            if (recordIndex == 0)
            {
                if (System.IO.File.Exists(logoPath))
                {
                    using (Image logo = Image.FromFile(logoPath))
                        e.Graphics.DrawImage(logo, new Rectangle(30, 22, 120, 86));
                }

                int dynTop = 120;

                e.Graphics.DrawString("Nom : 8341855 Canada Inc", contentFont, Brushes.Black, 30, dynTop, L);
                dynTop += lineHeight + 5;

                string type = transactions.Count > 0 ? (transactions[0].TransactionType ?? "") : "";
                e.Graphics.DrawString("Type: " + type, contentFont, Brushes.Black, 30, dynTop, L);
                dynTop += lineHeight + 5;

                e.Graphics.DrawString("Total : " + CalculateTotal(), contentFont, Brushes.Black, 30, dynTop, L);

                string refDate = "";
                if (transactions.Count > 0)
                {
                    var dt = ParseDateTime(transactions[0].LastModified);
                    refDate = FormatDate(dt);
                }
                var refRect = new RectangleF(e.MarginBounds.Right - 200, 30, 190, lineHeight);
                e.Graphics.DrawString("Référence : " + refDate, contentFont, Brushes.Black, refRect, R);

                topMargin = dynTop + 40;
            }

            // En-têtes colonnes
            for (int i = 0; i < headers.Length; i++)
            {
                var fmt = (i == 2) ? R : ((i == 3 || i == 4) ? C : L);
                e.Graphics.DrawString(headers[i], headerFont, Brushes.Black,
                    new RectangleF(colPos[i], topMargin, columnWidths[i], lineHeight), fmt);
            }
            topMargin += lineHeight;

            // Lignes
            int itemsPerPage = Math.Max(1, (e.MarginBounds.Height - topMargin - 60) / lineHeight);
            while (recordIndex < transactions.Count && itemsPerPage > 0)
            {
                var tx = transactions[recordIndex];
                string[] row =
                {
                    tx.ClientReferenceNumber ?? "",
                    tx.FullName ?? "",
                    FormatCurrency(ParseDecimal(tx.CreditAmount)),
                    FormatDate(ParseDateTime(tx.LastModified)),
                    FormatDate(ParseDateTime(tx.TransactionDateTime)),
                    tx.TransactionErrorCode ?? "",
                    tx.TransactionFailureReason ?? ""
                };
                for (int i = 0; i < row.Length; i++)
                {
                    var fmt = (i == 2) ? R : ((i == 3 || i == 4) ? C : L);
                    e.Graphics.DrawString(row[i], contentFont, Brushes.Black,
                        new RectangleF(colPos[i], topMargin, columnWidths[i], lineHeight), fmt);
                }

                recordIndex++;
                topMargin += lineHeight;
                itemsPerPage--;
            }

            // Pied de page (ligne + Date/ Page)
            int footerTextHeight = 18;
            int footerPadding = 6;
            int footerTextY = e.MarginBounds.Bottom - footerTextHeight;
            int footerLineY = footerTextY - footerPadding;

            e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, footerLineY, e.MarginBounds.Right, footerLineY);

            e.Graphics.DrawString($"Date : {DateTime.Now:dd/MM/yyyy}", footerFont, Brushes.Black,
                e.MarginBounds.Left + 10, footerTextY);

            var rightRect = new RectangleF(e.MarginBounds.Right - 80, footerTextY, 80, footerTextHeight);
            var rightFmt = new StringFormat { Alignment = StringAlignment.Far };
            e.Graphics.DrawString($"Page {pageCounter}", footerFont, Brushes.Black, rightRect, rightFmt);

            e.HasMorePages = recordIndex < transactions.Count;
            if (e.HasMorePages) pageCounter++; else pageCounter = 1;
        }

        private static decimal ParseDecimal(string s)
            => decimal.TryParse(s, NumberStyles.Any, new CultureInfo("fr-CA"), out var d) ? d : 0m;

        private static DateTime? ParseDateTime(string s)
            => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt) ? (DateTime?)dt : null;

        private static string FormatCurrency(decimal val)
            => val.ToString("N2", new CultureInfo("fr-CA")) + " $";

        private static string FormatDate(DateTime? dt)
            => dt.HasValue ? dt.Value.ToString("dd/MM/yyyy") : "";

        private string CalculateTotal()
        {
            decimal total = 0m;
            foreach (var t in transactions)
                total += ParseDecimal(t.CreditAmount);
            return FormatCurrency(total);
        }
    }
}