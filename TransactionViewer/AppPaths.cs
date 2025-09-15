using System;
using System.IO;

namespace TransactionViewer
{
    /// <summary>
    /// Centralise les chemins (lit Properties.Settings.Default avec fallback propre).
    /// </summary>
    internal static class AppPaths
    {
        public static string OutputCsvFolder
        {
            get
            {
                var v = Properties.Settings.Default.OutputCsvFolder?.Trim();
                if (!string.IsNullOrWhiteSpace(v)) return v;

                // Défaut: Documents\TransactionViewer\Output\NSF
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "TransactionViewer", "Output", "NSF");
            }
        }

        public static string ArchiveFolder
        {
            get
            {
                var v = Properties.Settings.Default.ArchiveFolder?.Trim();
                if (!string.IsNullOrWhiteSpace(v)) return v;

                // Défaut: Documents\TransactionViewer\Archive
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "TransactionViewer", "Archive");
            }
        }
    }
}
