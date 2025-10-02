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
        private readonly List<Transaction> transactions;
        private int recordIndex = 0;
        private int pageCounter = 1;

        private readonly int[] colWidths = { 90, 280, 90, 110, 110, 60, 220 };
        private readonly string[] headers = { "# Client", "Nom", "Montant", "Transmis Le", "Date Exception", "Code", "Raison" };

        public PrintManagerException(List<Transaction> list)
        {
            transactions = list ?? new List<Transaction>();
        }

        public void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            if (sender is PrintDocument doc) doc.DefaultPageSettings.Landscape = true;

            Font hf = new Font("Arial", 10, FontStyle.Bold);
            Font tf = new Font("Arial", 14, FontStyle.Bold);
            Font cf = new Font("Arial", 9);

            StringFormat L = new StringFormat { Alignment = StringAlignment.Near };
            StringFormat C = new StringFormat { Alignment = StringAlignment.Center };

            int[] pos = new int[colWidths.Length]; pos[0] = 70; for (int i = 1; i < colWidths.Length; i++) pos[i] = pos[i - 1] + colWidths[i - 1];

            int top = recordIndex == 0 ? 140 : 100;
            int ipp = (e.MarginBounds.Height - top - 40) / 18;

            e.Graphics.DrawString("Rapport de Transactions - Exceptions", tf, Brushes.Black, e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 50, C);

            for (int i = 0; i < headers.Length; i++)
                e.Graphics.DrawString(headers[i], hf, Brushes.Black, new RectangleF(pos[i], top, colWidths[i], 18), (i == 3 || i == 4) ? C : L);
            top += 18;

            while (recordIndex < transactions.Count && ipp > 0)
            {
                var tx = transactions[recordIndex];
                string[] row =
                {
                    tx.ClientReferenceNumber ?? "",
                    tx.FullName ?? "",
                    FormatCurrency(ParseDecimal(tx.CreditAmount)),
                    FormatDate(ParseDateTime(tx.TransactionDateTime)),
                    FormatDate(ParseDateTime(tx.LastModified)),
                    tx.TransactionErrorCode ?? "",
                    tx.TransactionFailureReason ?? ""
                };
                for (int i = 0; i < row.Length; i++)
                    e.Graphics.DrawString(row[i], cf, Brushes.Black, new RectangleF(pos[i], top, colWidths[i], 18), (i == 3 || i == 4) ? C : L);

                recordIndex++; top += 18; ipp--;
            }

            e.Graphics.DrawString($"Page {pageCounter}", cf, Brushes.Black, e.MarginBounds.Right - 60, e.MarginBounds.Bottom - 20, C);
            e.Graphics.DrawString($"Date : {DateTime.Now:dd/MM/yyyy}", cf, Brushes.Black, e.MarginBounds.Left + 10, e.MarginBounds.Bottom - 20, L);
            e.HasMorePages = recordIndex < transactions.Count;
            if (e.HasMorePages) pageCounter++; else pageCounter = 1;
        }

        private static decimal ParseDecimal(string s) => decimal.TryParse(s, NumberStyles.Any, new CultureInfo("fr-CA"), out var d) ? d : 0;
        private static DateTime? ParseDateTime(string s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : (DateTime?)null;
        private static string FormatCurrency(decimal val) => val.ToString("N2", new CultureInfo("fr-CA")) + " $";
        private static string FormatDate(DateTime? dt) => dt.HasValue ? dt.Value.ToString("dd/MM/yyyy") : "";
    }
}