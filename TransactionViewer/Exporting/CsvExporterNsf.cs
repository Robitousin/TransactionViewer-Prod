// Exporting/CsvExporterNsf.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TransactionViewer.Models;

namespace TransactionViewer.Exporting
{
    public static class CsvExporterNsf
    {
        /// <summary>
        /// Exporte un CSV NSF et retourne le chemin complet du fichier généré.
        /// - #Client = ClientReferenceNumber sinon ClientAccountID si 100% numérique
        /// - Montant en fr-CA sans symbole ($)
        /// - Dates au format dd-MM-yyyy
        /// - Colonnes: Client,Nom,Montant,DateNSF,TransmisLe,Code,Raison,TransactionID
        /// </summary>
        public static string Export(List<Transaction> txList, string outputRoot = null)
        {
            if (txList == null || txList.Count == 0)
                throw new InvalidOperationException("Aucune transaction à exporter (NSF).");

            // Dossier de sortie par défaut: %USERPROFILE%\Documents\NSF
            string root = !string.IsNullOrWhiteSpace(outputRoot)
                ? outputRoot
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NSF");

            Directory.CreateDirectory(root);

            string fileName = $"NSF_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.Combine(root, fileName);

            // Triage cohérent avec l’impression NSF : par LastModified croissant si possible
            var ordered = txList
                .OrderBy(t => TryParseDate(t.LastModified) ?? DateTime.MinValue)
                .ThenBy(t => t.TransactionID ?? string.Empty)
                .ToList();

            using (var sw = new StreamWriter(fullPath, false, Encoding.UTF8))
            {
                // En-tête
                sw.WriteLine("Client,Nom,Montant,DateNSF,TransmisLe,Code,Raison,TransactionID");

                foreach (var tx in ordered)
                {
                    string client = GetClientRef(tx);
                    string nom = Safe(tx.FullName);
                    string montant = FormatAmountFr(tx.CreditAmount);
                    string dateNsf = OnlyDate(tx.LastModified);
                    string transmis = OnlyDate(tx.TransactionDateTime);
                    string code = Safe(tx.TransactionErrorCode);
                    string raison = Safe(tx.TransactionFailureReason);
                    string tid = Safe(tx.TransactionID);

                    sw.WriteLine(string.Join(",",
                        Q(client), Q(nom), Q(montant), Q(dateNsf), Q(transmis), Q(code), Q(raison), Q(tid)
                    ));
                }
            }

            return fullPath;
        }

        // ===== Helpers CSV =====

        private static string GetClientRef(Transaction tx)
        {
            var refNum = (tx.ClientReferenceNumber ?? "").Trim();
            if (!string.IsNullOrEmpty(refNum)) return refNum;

            var acc = (tx.ClientAccountID ?? "").Trim();
            return Regex.IsMatch(acc, @"^\d+$") ? acc : "";
        }

        private static string FormatAmountFr(string input)
        {
            // Sortie CSV : 144,00 (fr-CA), sans symbole $
            var ci = new CultureInfo("fr-CA");
            if (decimal.TryParse((input ?? "").Trim(), NumberStyles.Any, ci, out var d))
                return d.ToString("0.00", ci);

            // Essai invariant si la source est "1.23"
            if (decimal.TryParse((input ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var inv))
                return inv.ToString("0.00", ci);

            return "";
        }

        private static string OnlyDate(string input)
        {
            var dt = TryParseDate(input);
            return dt.HasValue ? dt.Value.ToString("dd-MM-yyyy") : "";
        }

        private static DateTime? TryParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            // Invariant pour accepter "yyyy-MM-dd HH:mm:ss"
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            // Fallback culture locale
            if (DateTime.TryParse(s, out dt))
                return dt;
            return null;
        }

        private static string Safe(string s) => (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();

        private static string Q(string s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";
    }
}
