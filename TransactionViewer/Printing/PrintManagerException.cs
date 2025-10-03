using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using TransactionViewer.Models;

namespace TransactionViewer.Printing
{
    /// <summary>
    /// Impression des Exceptions (paysage), avec cartouche 1ère page (Nom, Type, Total, Référence à droite),
    /// ligne d’en-tête sous les titres, colonnes normalisées et pied de page harmonisé.
    /// Compatible C# 7.3 (pas de using var).
    /// </summary>
    public class PrintManagerException : IPrintManager
    {
        private readonly List<Transaction> transactions;
        private int recordIndex = 0;
        private int pageCounter = 1;

        // Colonnes Exceptions (ordre + largeurs)
        private readonly int[] columnWidths = { 90, 280, 90, 110, 110, 60, 220 };
        private readonly string[] headers = {
            "# Client", "Nom du Client", "Montant", "Transmis Le", "Date Exception", "Code", "Raison"
        };

        private readonly string logoPath = @"Resources\\logo.png";
        private static Image cachedLogo; // logo mis en cache pour éviter IO disque répétées

        public PrintManagerException(List<Transaction> list)
        {
            transactions = list ?? new List<Transaction>();
        }

        public void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
        {
            // Forcer paysage
            if (sender is PrintDocument doc)
                doc.DefaultPageSettings.Landscape = true;

            // ===== Styles =====
            int lineHeight = 18;
            using (Font headerFont = new Font("Arial", 10, FontStyle.Bold))
            using (Font titleFont = new Font("Arial", 14, FontStyle.Bold))
            using (Font contentFont = new Font("Arial", 9))
            using (Font footerFont = new Font("Arial", 8))
            {
                StringFormat L = new StringFormat { Alignment = StringAlignment.Near };
                StringFormat C = new StringFormat { Alignment = StringAlignment.Center };
                StringFormat R = new StringFormat { Alignment = StringAlignment.Far };

                // ===== Colonnes =====
                int[] colPos = new int[columnWidths.Length];
                colPos[0] = 70;
                for (int i = 1; i < columnWidths.Length; i++)
                    colPos[i] = colPos[i - 1] + columnWidths[i - 1];

                // ===== Titres =====
                e.Graphics.DrawString("Rapport de Transactions", titleFont, Brushes.Black,
                    e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 70, C);
                e.Graphics.DrawString("Exceptions", titleFont, Brushes.Black,
                    e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 45, C);

                // Ligne d’en-tête (sous les titres)
                int headerLineY = e.MarginBounds.Top - 10;
                e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, headerLineY, e.MarginBounds.Right, headerLineY);

                // ===== Cartouche 1re page =====
                int topMargin = 90; // valeur pour pages suivantes
                if (recordIndex == 0)
                {
                    // Logo en cache
                    if (cachedLogo == null && System.IO.File.Exists(logoPath))
                    {
                        using (var fs = new System.IO.FileStream(logoPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                        {
                            cachedLogo = Image.FromStream(fs);
                        }
                    }
                    if (cachedLogo != null)
                        e.Graphics.DrawImage(cachedLogo, new Rectangle(30, 22, 120, 86));

                    int dynTop = 120;

                    // Nom (fixe pour l’instant)
                    e.Graphics.DrawString("Nom : 8341855 Canada Inc", contentFont, Brushes.Black, 30, dynTop, L);
                    dynTop += lineHeight + 5;

                    // Type : d’après la 1ère transaction si présent
                    string type = (transactions.Count > 0) ? (transactions[0].TransactionType ?? string.Empty) : string.Empty;
                    e.Graphics.DrawString("Type: " + type, contentFont, Brushes.Black, 30, dynTop, L);
                    dynTop += lineHeight + 5;

                    // Total (somme des CreditAmount)
                    e.Graphics.DrawString("Total : " + CalculateTotal(), contentFont, Brushes.Black, 30, dynTop, L);

                    // Référence à droite (on prend LastModified comme date de référence des Exceptions)
                    string refDate = string.Empty;
                    if (transactions.Count > 0)
                    {
                        DateTime? dt = ParseDateTime(transactions[0].LastModified);
                        refDate = FormatDate(dt);
                    }
                    RectangleF refRect = new RectangleF(e.MarginBounds.Right - 200, 30, 190, lineHeight);
                    e.Graphics.DrawString("Référence : " + refDate, contentFont, Brushes.Black, refRect, R);

                    // Descendre sous le cartouche
                    topMargin = dynTop + 40;
                }

                // ===== En-têtes colonnes =====
                for (int i = 0; i < headers.Length; i++)
                {
                    // Montant à droite, dates centrées
                    StringFormat fmt = (i == 2) ? R : (i == 3 || i == 4 ? C : L);
                    e.Graphics.DrawString(headers[i], headerFont, Brushes.Black,
                        new RectangleF(colPos[i], topMargin, columnWidths[i], lineHeight), fmt);
                }
                topMargin += lineHeight;

                // ===== Lignes =====
                int itemsPerPage = Math.Max(1, (e.MarginBounds.Height - topMargin - 60) / lineHeight);
                while (recordIndex < transactions.Count && itemsPerPage > 0)
                {
                    Transaction tx = transactions[recordIndex];

                    string client = tx.ClientReferenceNumber ?? string.Empty;
                    string nom = tx.FullName ?? string.Empty;
                    string montant = FormatCurrency(ParseDecimal(tx.CreditAmount));
                    string transLe = FormatDate(ParseDateTime(tx.TransactionDateTime));
                    string dateExc = FormatDate(ParseDateTime(tx.LastModified));
                    string code = tx.TransactionErrorCode ?? string.Empty;
                    string raison = tx.TransactionFailureReason ?? string.Empty;

                    string[] row = { client, nom, montant, transLe, dateExc, code, raison };

                    for (int i = 0; i < row.Length; i++)
                    {
                        StringFormat fmt = (i == 2) ? R : (i == 3 || i == 4 ? C : L);
                        e.Graphics.DrawString(row[i], contentFont, Brushes.Black,
                            new RectangleF(colPos[i], topMargin, columnWidths[i], lineHeight), fmt);
                    }

                    recordIndex++;
                    topMargin += lineHeight;
                    itemsPerPage--;
                }

                // ===== Pied de page (ligne + Date/ Page) =====
                int footerTextHeight = 18;
                int footerPadding = 6;
                int footerTextY = e.MarginBounds.Bottom - footerTextHeight;
                int footerLineY = footerTextY - footerPadding;

                // Ligne au-dessus du texte
                e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, footerLineY, e.MarginBounds.Right, footerLineY);

                // Date (gauche)
                e.Graphics.DrawString("Date : " + DateTime.Now.ToString("dd/MM/yyyy"), footerFont, Brushes.Black,
                    e.MarginBounds.Left + 10, footerTextY);

                // Page (droite)
                RectangleF rightRect = new RectangleF(e.MarginBounds.Right - 80, footerTextY, 80, footerTextHeight);
                StringFormat rightFmt = new StringFormat { Alignment = StringAlignment.Far };
                e.Graphics.DrawString("Page " + pageCounter, footerFont, Brushes.Black, rightRect, rightFmt);

                // Pagination
                e.HasMorePages = recordIndex < transactions.Count;
                if (e.HasMorePages) pageCounter++; else pageCounter = 1;
            }
        }

        // ===== Utils =====
        private static decimal ParseDecimal(string s)
        {
            return string.IsNullOrWhiteSpace(s)
                ? 0m
                : (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("fr-CA"), out decimal d) ? d : 0m);
        }

        private static DateTime? ParseDateTime(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt) ? (DateTime?)dt : null;
        }

        private static string FormatCurrency(decimal val)
        {
            return val.ToString("N2", new CultureInfo("fr-CA")) + " $";
        }

        private static string FormatDate(DateTime? dt)
        {
            return dt.HasValue ? dt.Value.ToString("dd/MM/yyyy") : string.Empty;
        }

        private string CalculateTotal()
        {
            decimal total = 0m;
            foreach (var t in transactions)
                total += ParseDecimal(t.CreditAmount);
            return FormatCurrency(total);
        }
    }
}

