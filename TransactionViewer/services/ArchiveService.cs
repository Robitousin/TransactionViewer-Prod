using System;
using System.IO;

namespace TransactionViewer.Services
{
    public static class ArchiveService
    {
        /// <summary>
        /// Déplace le fichier dans ...\Archive\NSF\yyyy-MM\NomFichier (et rend le nom unique si besoin).
        /// </summary>
        public static string MoveToNsfArchive(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Fichier introuvable pour archivage.", filePath);

            string baseArchive = AppPaths.ArchiveFolder;
            string month = DateTime.Now.ToString("yyyy-MM");
            string destDir = Path.Combine(baseArchive, "NSF", month);
            Directory.CreateDirectory(destDir);

            string destPath = Path.Combine(destDir, Path.GetFileName(filePath));
            destPath = EnsureUnique(destPath);

            File.Move(filePath, destPath);
            return destPath;
        }

        private static string EnsureUnique(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            int i = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{name} ({i}){ext}");
                i++;
            } while (File.Exists(candidate));

            return candidate;
        }
    }
}
