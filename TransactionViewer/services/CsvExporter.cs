using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using TransactionViewer.Models;

namespace TransactionViewer.Services
{
    public static class CsvExporter
    {
        /// <summary>
        /// Export NSF selon le "Format CSV Final Verrouillé":
        /// - Encodage: Windows-1252 (ANSI)
        /// - Sauts de ligne: CRLF
        /// - Séparateur: virgule
        /// - En-tête: aucun
        /// - 10 champs mappés + insertion de ',,,' après le 5e champ
        /// - Ajout '" "' final (=> ,"" en fin de ligne)
        /// - Champs non vides entre guillemets; champs vides = rien (,,)
        /// </summary>
        public static string ExportTransactionsToCsvLockedFormat(
            IEnumerable<Transaction> transactions,
            string destinationFilePath = null,
            string dateFormat = "yyyy-MM-dd")
        {
            if (transactions == null) throw new ArgumentNullException(nameof(transactions));

            string outputDir = AppPaths.OutputCsvFolder;
            Directory.CreateDirectory(outputDir);

            if (string.IsNullOrWhiteSpace(destinationFilePath))
            {
                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                destinationFilePath = Path.Combine(outputDir, $"NSF_{stamp}.csv");
            }

            var enc = Encoding.GetEncoding(1252);
            var ciFr = new CultureInfo("fr-CA"); // 1 234,56

            using (var fs = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(fs, enc))
            {
                foreach (var t in transactions)
                {
                    // ====== Mappage des 10 champs ======
                    // 1: TransactionID
                    var c1 = QuoteIfNotEmpty(t?.TransactionID);

                    // 2: ClientReferenceNumber
                    var c2 = QuoteIfNotEmpty(t?.ClientReferenceNumber);

                    // 3: FullName (fallback AccountName si vide)
                    var fullName = string.IsNullOrWhiteSpace(t?.FullName) ? t?.AccountName : t?.FullName;
                    var c3 = QuoteIfNotEmpty(fullName);

                    // 4: Montant = CreditAmount (sinon DebitAmount) -> "xx,yy $"
                    var c4 = QuoteIfNotEmpty(FormatAmountFrCa(t?.CreditAmount, t?.DebitAmount, ciFr));

                    // 5: Type = "Débit" si "EFT Funding" sinon "Crédit"
                    var c5 = QuoteIfNotEmpty(MapTypeToDebitCredit(t?.TransactionType));

                    // >>> Insérer deux champs vides après le 5e: ,,, <<<
                    var empties = new[] { "", "" };

                    // 6: ScheduledTransactionID
                    var c6 = QuoteIfNotEmpty(t?.ScheduledTransactionID);

                    // 7: TransactionDateTime (date seule)
                    var c7 = QuoteIfNotEmpty(OnlyDate(t?.TransactionDateTime, dateFormat));

                    // 8: LastModified (date seule)
                    var c8 = QuoteIfNotEmpty(OnlyDate(t?.LastModified, dateFormat));

                    // 9: TransactionErrorCode
                    var c9 = QuoteIfNotEmpty(t?.TransactionErrorCode);

                    // 10: TransactionFailureReason
                    var c10 = QuoteIfNotEmpty(t?.TransactionFailureReason);

                    // Concaténation: 1..5, ,,, , 6..10, + ,"" final
                    var parts = new List<string> { c1, c2, c3, c4, c5 };
                    parts.AddRange(empties);               // ,,, après 5
                    parts.AddRange(new[] { c6, c7, c8, c9, c10 });

                    // Ligne sans en-tête, CRLF, et champ final "" (spécial)
                    string line = string.Join(",", parts) + ",\"\"";
                    writer.Write(line + "\r\n");
                }
            }

            return destinationFilePath;
        }

        // -------- Helpers --------

        private static string QuoteIfNotEmpty(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return ""; // champ vide => pas de guillemets
            s = s.Replace("\"", "\"\"");                // escape des guillemets internes
            return $"\"{s}\"";
        }

        private static string OnlyDate(string s, string dateFormat)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            // couper à l'espace si "YYYY-MM-DD HH:mm:ss"
            int sp = s.IndexOf(' ');
            if (sp > 0) s = s.Substring(0, sp);

            if (DateTime.TryParse(s, out var dt))
                return dt.ToString(dateFormat);
            return s; // fallback brut
        }

        private static string MapTypeToDebitCredit(string transactionType)
        {
            return (!string.IsNullOrWhiteSpace(transactionType) &&
                    transactionType.Trim().Equals("EFT Funding", StringComparison.OrdinalIgnoreCase))
                ? "Débit"
                : "Crédit";
        }

        private static string FormatAmountFrCa(string creditRaw, string debitRaw, CultureInfo ciFr)
        {
            // Choisir credit sinon debit
            var raw = !string.IsNullOrWhiteSpace(creditRaw) ? creditRaw : debitRaw;
            if (string.IsNullOrWhiteSpace(raw)) return "";

            if (!TryParseFlexible(raw, out var val)) return "";

            // "xx,yy $" (séparateurs fr-CA)
            return string.Format(ciFr, "{0:N2}", val) + " $";
        }

        private static bool TryParseFlexible(string raw, out decimal value)
        {
            // Nettoyage
            var s = (raw ?? "").Trim()
                .Replace('\u00A0', ' ')      // NBSP -> espace normal
                .Replace("$", "")
                .Replace("CAD", "")
                .Trim();

            // 1) fr-CA
            if (decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, new CultureInfo("fr-CA"), out value))
                return true;

            // 2) tente "1 234,56" -> "1234.56"
            var s2 = s.Replace(" ", "").Replace(",", ".");
            if (decimal.TryParse(s2, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out value))
                return true;

            // 3) retire virgules de milliers
            var s3 = s.Replace(",", "");
            if (decimal.TryParse(s3, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out value))
                return true;

            value = 0m;
            return false;
        }
    }
}
