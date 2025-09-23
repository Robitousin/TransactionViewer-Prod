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

            string outputDir;

            // **Blindage** : si aucun chemin fourni, utilise le PROFIL COURANT (Documents\TransactionViewer\NSF).
            if (string.IsNullOrWhiteSpace(destinationFilePath))
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                outputDir = Path.Combine(docs, "TransactionViewer", "NSF");
                Directory.CreateDirectory(outputDir);

                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                destinationFilePath = Path.Combine(outputDir, $"NSF_{stamp}.csv");
            }
            else
            {
                outputDir = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrWhiteSpace(outputDir))
                    Directory.CreateDirectory(outputDir);
            }

            var enc = Encoding.GetEncoding(1252);
            var ciFr = new CultureInfo("fr-CA"); // 1 234,56

            using (var fs = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(fs, enc))
            {
                foreach (var t in transactions)
                {
                    // ====== Mappage des 10 champs ======
                    var c1 = QuoteIfNotEmpty(t?.TransactionID);                // 1
                    var c2 = QuoteIfNotEmpty(t?.ClientReferenceNumber);        // 2

                    var fullName = string.IsNullOrWhiteSpace(t?.FullName) ? t?.AccountName : t?.FullName;
                    var c3 = QuoteIfNotEmpty(fullName);                         // 3

                    var c4 = QuoteIfNotEmpty(FormatAmountFrCa(t?.CreditAmount, t?.DebitAmount, ciFr)); // 4
                    var c5 = QuoteIfNotEmpty(MapTypeToDebitCredit(t?.TransactionType));                // 5

                    var empties = new[] { "", "" };                            // ,,

                    var c6 = QuoteIfNotEmpty(t?.ScheduledTransactionID);       // 6
                    var c7 = QuoteIfNotEmpty(OnlyDate(t?.TransactionDateTime, dateFormat)); // 7
                    var c8 = QuoteIfNotEmpty(OnlyDate(t?.LastModified, dateFormat));        // 8
                    var c9 = QuoteIfNotEmpty(t?.TransactionErrorCode);         // 9
                    var c10 = QuoteIfNotEmpty(t?.TransactionFailureReason);    // 10

                    var parts = new List<string> { c1, c2, c3, c4, c5 };
                    parts.AddRange(empties);                                   // ,,
                    parts.AddRange(new[] { c6, c7, c8, c9, c10 });

                    string line = string.Join(",", parts) + ",\"\"";
                    writer.Write(line + "\r\n");
                }
            }

            return destinationFilePath;
        }

        // -------- Helpers --------

        private static string QuoteIfNotEmpty(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return ""; // champ vide
            s = s.Replace("\"", "\"\"");
            return $"\"{s}\"";
        }

        private static string OnlyDate(string s, string dateFormat)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            int sp = s.IndexOf(' ');
            if (sp > 0) s = s.Substring(0, sp);

            if (DateTime.TryParse(s, out var dt))
                return dt.ToString(dateFormat);
            return s;
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
            var raw = !string.IsNullOrWhiteSpace(creditRaw) ? creditRaw : debitRaw;
            if (string.IsNullOrWhiteSpace(raw)) return "";

            if (!TryParseFlexible(raw, out var val)) return "";
            return string.Format(ciFr, "{0:N2}", val) + " $";
        }

        private static bool TryParseFlexible(string raw, out decimal value)
        {
            var s = (raw ?? "").Trim()
                .Replace('\u00A0', ' ')
                .Replace("$", "")
                .Replace("CAD", "")
                .Trim();

            if (decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, new CultureInfo("fr-CA"), out value))
                return true;

            var s2 = s.Replace(" ", "").Replace(",", ".");
            if (decimal.TryParse(s2, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out value))
                return true;

            var s3 = s.Replace(",", "");
            if (decimal.TryParse(s3, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out value))
                return true;

            value = 0m;
            return false;
        }
    }
}
