using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms; // MessageBox
using TransactionViewer.Models;

namespace TransactionViewer
{
/// <summary>
/// Impression NSF (paysage) – CONTRAT aligné sur docs/PrintLayouts.md
/// En-têtes fixés + auto-vérification avant rendu. Tri montant ascendant.
/// </summary>
public class PrintManagerFailed : IPrintManager
{
// ===== Contrat colonnes (docs/PrintLayouts.md) =====
private static readonly string[] Headers =
{
"# Client", "Nom du Client", "Montant", "Date NSF", "Transmis Le", "Code", "NSF Raison"
};

```
    private readonly int[] columnWidths = { 90, 280, 90, 110, 110, 60, 220 };

    private readonly List<Transaction> transactions;
    private int recordIndex = 0;
    private int pageCounter = 1;

    private const string LogoPath = @"Resources\logo.png";
    private const string CompanyName = "8341855 Canada Inc";
    private const string DateFormat = "dd/MM/yyyy";
    private static readonly CultureInfo CiFrCa = new CultureInfo("fr-CA");

    public PrintManagerFailed(List<Transaction> list)
    {
        // Tri par montant ascendant (contrat)
        transactions = (list ?? new List<Transaction>())
            .OrderBy(t => ParseDecimal(t.CreditAmount))
            .ToList();
    }

    public void PrintDocument_PrintPage(object sender, PrintPageEventArgs e)
    {
        // Forcer paysage si non configuré
        if (sender is PrintDocument doc) doc.DefaultPageSettings.Landscape = true;

        int lineHeight = 18;
        Font headerFont = new Font("Arial", 10, FontStyle.Bold);
        Font titleFont  = new Font("Arial", 14, FontStyle.Bold);
        Font contentFont= new Font("Arial", 9);
        Font footerFont = new Font("Arial", 8);

        StringFormat L = new StringFormat { Alignment = StringAlignment.Near };
        StringFormat C = new StringFormat { Alignment = StringAlignment.Center };
        StringFormat R = new StringFormat { Alignment = StringAlignment.Far };

        int[] colPos = new int[columnWidths.Length];
        colPos[0] = 70;
        for (int i = 1; i < columnWidths.Length; i++) colPos[i] = colPos[i - 1] + columnWidths[i - 1];

        // Auto-check contrat
        if (!ValidateHeaders(Headers))
        {
            MessageBox.Show(
                "Incohérence d'en-têtes (NSF). Vérifiez docs/PrintLayouts.md.",
                "Mise en page",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        int topMargin = recordIndex == 0 ? 120 : 90;
        int itemsPerPage = (e.MarginBounds.Height - topMargin - 40) / lineHeight;

        // Titres
        e.Graphics.DrawString("Rapport de Transactions", titleFont, Brushes.Black,
            e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 70, C);
        e.Graphics.DrawString("NSF", titleFont, Brushes.Black,
            e.MarginBounds.Left + (e.MarginBounds.Width / 2), e.MarginBounds.Top - 45, C);

        // Cartouche 1re page
        if (recordIndex == 0)
        {
            if (System.IO.File.Exists(LogoPath))
            {
                Image logo = Image.FromFile(LogoPath);
                e.Graphics.DrawImage(logo, new Rectangle(30, 22, 120, 86));
                logo.Dispose();
            }

            int dynTop = 130;
            e.Graphics.DrawString($"Nom : {CompanyName}", contentFont, Brushes.Black, 30, dynTop, L);
            dynTop += lineHeight + 5;

            string type = transactions.Count > 0 ? transactions[0].TransactionType ?? string.Empty : string.Empty;
            e.Graphics.DrawString($"Type: {type}", contentFont, Brushes.Black, 30, dynTop, L);
            dynTop += lineHeight + 5;

            e.Graphics.DrawString($"Total : {CalculateTotal()}", contentFont, Brushes.Black, 30, dynTop, L);

            string refDate = string.Empty;
            if (transactions.Count > 0)
            {
                var dt = ParseDateTime(transactions[0].LastModified);
                refDate = FormatDate(dt);
            }
            e.Graphics.DrawString($"Référence : {refDate}", contentFont, Brushes.Black,
                e.MarginBounds.Right - 10, 30, R);

            topMargin = dynTop + 40;
        }

        // En-têtes (contrat)
        for (int i = 0; i < Headers.Length; i++)
        {
            var fmt = (i == 2) ? R : ((i == 3 || i == 4) ? C : L); // Montant droite, dates centrées
            e.Graphics.DrawString(Headers[i], headerFont, Brushes.Black,
                new RectangleF(colPos[i], topMargin, columnWidths[i], lineHeight), fmt);
        }
        topMargin += lineHeight;

        // Lignes
        while (recordIndex < transactions.Count && itemsPerPage > 0)
        {
            var tx = transactions[recordIndex];

            string client  = tx.ClientReferenceNumber ?? string.Empty;
            string nom     = tx.FullName ?? string.Empty;
            string montant = FormatCurrency(ParseDecimal(tx.CreditAmount));
            string dateNsf = FormatDate(ParseDateTime(tx.LastModified));
            string transLe = FormatDate(ParseDateTime(tx.TransactionDateTime));
            string code    = tx.TransactionErrorCode ?? string.Empty;
            string raison  = tx.TransactionFailureReason ?? string.Empty;

            string[] row = { client, nom, montant, dateNsf, transLe, code, raison };
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

        // Pied de page
        int footY = e.MarginBounds.Bottom - 20;
        e.Graphics.DrawLine(Pens.Black, e.MarginBounds.Left, footY - 10, e.MarginBounds.Right, footY - 10);
        e.Graphics.DrawString($"Page {pageCounter}", footerFont, Brushes.Black, e.MarginBounds.Right - 50, footY - 5, R);
        e.Graphics.DrawString($"Date : {DateTime.Now:dd/MM/yyyy}", footerFont, Brushes.Black, e.MarginBounds.Left + 10, footY - 5, L);

        e.HasMorePages = recordIndex < transactions.Count;
        if (e.HasMorePages) pageCounter++; else pageCounter = 1;
    }

    // ===== Utils =====
    private static bool ValidateHeaders(string[] expected)
    {
        return expected != null && expected.Length == 7
            && expected[0] == "# Client"
            && expected[1] == "Nom du Client"
            && expected[2] == "Montant"
            && expected[3] == "Date NSF"
            && expected[4] == "Transmis Le"
            && expected[5] == "Code"
            && expected[6] == "NSF Raison";
    }

    private static decimal ParseDecimal(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        decimal d; return decimal.TryParse(s, NumberStyles.Any, CiFrCa, out d) ? d : 0m;
    }

    private static DateTime? ParseDateTime(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        DateTime dt; return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) ? (DateTime?)dt : null;
    }

    private static string FormatCurrency(decimal val) => val.ToString("N2", CiFrCa) + " $";
    private static string FormatDate(DateTime? dt) => dt.HasValue ? dt.Value.ToString(DateFormat) : string.Empty;

    private string CalculateTotal()
    {
        decimal total = 0m;
        foreach (var t in transactions) total += ParseDecimal(t.CreditAmount);
        return FormatCurrency(total);
    }
}
```

}
