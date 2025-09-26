// Helpers/ClientRefHelper.cs
using System.Text.RegularExpressions;
using TransactionViewer.Models;

namespace TransactionViewer.Helpers
{
    public static class ClientRefHelper
    {
        // Chiffres seulement (aucun espace, lettre, tiret, etc.)
        private static readonly Regex DigitsOnly = new Regex(@"^\d+$");

        public static string GetClientRef(Transaction tx)
        {
            if (tx == null) return "";
            var refNum = (tx.ClientReferenceNumber ?? "").Trim();
            if (!string.IsNullOrEmpty(refNum))
                return refNum;

            var accountId = (tx.ClientAccountID ?? "").Trim();
            if (!string.IsNullOrEmpty(accountId) && DigitsOnly.IsMatch(accountId))
                return accountId;

            return "";
        }
    }
}
