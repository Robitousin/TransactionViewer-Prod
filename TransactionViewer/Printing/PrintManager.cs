using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using TransactionViewer.Models;

namespace TransactionViewer
{
    public class PrintManager : IPrintManager
    {
        private readonly List<Transaction> transactions;
        private int recordIndex = 0;
        private int pageCounter = 1;

        private readonly int[] columnWidths = { 90, 280, 90, 110, 120 };
        private readonly string[] headers = { "# Client", "Nom du client", "Montant", "Transmis Le", "TransactionID" };
        private readonly string logoPath = @"Resources\\logo.png";

        public PrintManager(List<Transaction> transactions)
        {
            this.transactions = transactions ?? new List<Transaction>();
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

            int[] colPos = new int[columnWidths.Length];
            colPos[0] = 70;
            for (int i = 1; i < columnWidths.Length; i++)
                colPos[i] = colPos[i - 1] + columnWidths[i - 1];

            int topMargin = recordIndex == 0 ? 130 : 100;
            int itemsPerPage = (e.MarginBounds.Height - topMargin - 40) / lineHeight;

            e.Graphics.DrawString("Rapport de Transactions", titleFont, Brushes.Black,
                e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 70, centerAlign);
            e.Graphics.DrawString("Prélèvements", titleFont, Brushes.Black,
                e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 45, centerAlign);

            if (recordIndex == 0 && System.IO.File.Exists(logoPath))
            {
                Image logo = Image.FromFile(logoPath);
                e.Graphics.DrawImage(logo, new Rectangle(30, 22, 120, 86));
                logo.Dispose();
            }

            for (int i = 0; i < headers.Length; i++)
            {
                var fmt = (i >= 3) ? centerAlign : leftAlign;
                e.Graphics.DrawString(headers[i], headerFont, Brushes.Black,
                    new RectangleF(colPos[i], topMargin, columnWidths[i], lineHeight), fmt);
            }
            topMargin += lineHeight;

            while (recordIndex < transactions.Count && itemsPerPage > 0)
            {
                var tx = transactions[recordIndex];
                string[] row =
                {
                    tx.ClientReferenceNumber ?? "",
                    tx.FullName ?? "",
                    FormatCurrency(ParseDecimal(tx.CreditAmount)),
                    FormatDate(ParseDateTime(tx.TransactionDateTime)),
                    tx.TransactionID ?? ""
                };
                for (int i = 0; i < row.Length; i++)
                {
                    var fmt = (i >= 3) ? centerAlign : leftAlign;
                    e.Graphics.DrawString(row[i], contentFont, Brushes.Black,
                        new RectangleF(colPos[i], topMargin, columnWidths[i], lineHeight), fmt);
                }
                recordIndex++; topMargin += lineHeight; itemsPerPage--;
            }

            int footY = e.MarginBounds.Bottom - 20;
            e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, footY - 10, e.MarginBounds.Right, footY - 10);
            e.Graphics.DrawString($"Page {pageCounter}", footerFont, Brushes.Black, e.MarginBounds.Right - 50, footY - 5, rightAlign);
            e.Graphics.DrawString($"Date : {DateTime.Now:dd/MM/yyyy}", footerFont, Brushes.Black, e.MarginBounds.Left + 10, footY - 5, leftAlign);
            e.HasMorePages = recordIndex < transactions.Count;
            if (e.HasMorePages) pageCounter++; else pageCounter = 1;
        }

        private static decimal ParseDecimal(string s) => decimal.TryParse(s, NumberStyles.Any, new CultureInfo("fr-CA"), out var d) ? d : 0;
        private static DateTime? ParseDateTime(string s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : (DateTime?)null;
        private static string FormatCurrency(decimal val) => val.ToString("N2", new CultureInfo("fr-CA")) + " $";
        private static string FormatDate(DateTime? dt) => dt.HasValue ? dt.Value.ToString("dd/MM/yyyy") : "";
    }
}