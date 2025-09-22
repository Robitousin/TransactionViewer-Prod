using System;
using System.IO;

namespace TransactionViewer.Services
{
    public static class ArchiveService
    {
        /// <summary>
        /// Archive un CSV NSF vers un dossier racine donné.
        /// Crée un sous-dossier par date (yyyyMMdd) et renvoie le chemin final.
        /// </summary>
        public static string MoveToNsfArchive(string csvPath, string archiveRoot)
        {
            if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
                throw new FileNotFoundException("CSV introuvable pour archivage.", csvPath);

            if (string.IsNullOrWhiteSpace(archiveRoot))
            {
                // Fallback : Documents\TransactionViewer\Archive (profil courant)
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                archiveRoot = Path.Combine(docs, "TransactionViewer", "Archive");
            }

            // Sous-dossier par date pour garder les exports propres
            var dayFolder = Path.Combine(archiveRoot, DateTime.Now.ToString("yyyyMMdd"));
            Directory.CreateDirectory(dayFolder);

            var fileName = Path.GetFileName(csvPath);
            var target = Path.Combine(dayFolder, fileName);

            // Si le fichier existe déjà, suffixer l’heure pour uniqueness
            if (File.Exists(target))
            {
                var name = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                target = Path.Combine(dayFolder, $"{name}_{DateTime.Now:HHmmss}{ext}");
            }

            File.Move(csvPath, target);
            return target;
        }

        /// <summary>
        /// Rétro-compat : si du code appelle encore l’ancienne signature.
        /// </summary>
        public static string MoveToNsfArchive(string csvPath) =>
            MoveToNsfArchive(csvPath, archiveRoot: null);
    }
}
