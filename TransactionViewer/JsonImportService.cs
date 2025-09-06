using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TransactionViewer.DataAccess;
using TransactionViewer.Models;

namespace TransactionViewer
{
    public static class JsonImportService
    {
        public static void ImportTransactionsFromFile(string filePath)
        {
            // 1) Lire le fichier JSON
            string jsonContent = File.ReadAllText(filePath);
            var root = JsonConvert.DeserializeObject<RootObject>(jsonContent);
            if (root?.Transactions == null) return;

            // 2) Parcourir chaque transaction JSON
            foreach (var jt in root.Transactions)
            {
                // Facultatif : ne traiter que "EFT Funding"
                if (jt.TransactionType == "EFT Funding")
                {
                    // 3) Vérifier si la transaction existe en base
                    var existingTx = TransactionRepository.GetByTransactionID(jt.TransactionID);

                    if (existingTx != null)
                    {
                        // 4) Mettre à jour tous les champs
                        existingTx.AccountName = jt.AccountName;
                        existingTx.FullName = jt.FullName;
                        existingTx.TransactionDateTime = jt.TransactionDateTime;
                        existingTx.TransactionType = jt.TransactionType;
                        existingTx.TransactionStatus = jt.TransactionStatus;
                        existingTx.DebitAmount = jt.DebitAmount;
                        existingTx.CreditAmount = jt.CreditAmount;
                        existingTx.Currency = jt.Currency;
                        existingTx.HoldAmount = jt.HoldAmount;
                        existingTx.LastModified = jt.LastModified;
                        existingTx.ParentTransactionID = jt.ParentTransactionID;
                        existingTx.ClientReferenceNumber = jt.ClientReferenceNumber;
                        existingTx.ScheduledTransactionID = jt.ScheduledTransactionID;
                        existingTx.WalletID = jt.WalletID;
                        existingTx.WalletName1 = jt.WalletName1;
                        existingTx.WalletName2 = jt.WalletName2;
                        existingTx.ClientAccountID = jt.ClientAccountID;
                        existingTx.TransactionErrorCode = jt.TransactionErrorCode;
                        existingTx.TransactionFailureReason = jt.TransactionFailureReason;
                        existingTx.TransactionFlag = jt.TransactionFlag;
                        existingTx.ELinxRequestID = jt.ELinxRequestID;
                        existingTx.IsRefunded = jt.IsRefunded;
                        existingTx.IsRefund = jt.IsRefund;
                        existingTx.Notes = jt.Notes;
                        existingTx.ChildTransactionIDs = jt.ChildTransactionIDs;

                        // 5) LOGIQUE DE RÉACTIVATION
                        //    Si la transaction était déjà traitée (IsPrelevementDone || IsNSFDone)
                        //    et que le nouveau TransactionStatus "réouvre" la transaction
                        //    => on passe en Exception
                        if ((existingTx.IsPrelevementDone || existingTx.IsNSFDone)
                            && (jt.TransactionStatus == "in progress"
                                || jt.TransactionStatus == "reopen"))
                        {
                            // On annule le traitement
                            existingTx.IsPrelevementDone = false;
                            existingTx.IsNSFDone = false;
                            // On la place en "Exception"
                            existingTx.IsException = true;
                        }

                        // 6) Mise à jour en base
                        TransactionRepository.InsertOrUpdateTransaction(existingTx);
                    }
                    else
                    {
                        // 7) Nouvelle transaction -> on crée un objet localTx
                        var localTx = new Transaction
                        {
                            TransactionID = jt.TransactionID,
                            AccountName = jt.AccountName,
                            FullName = jt.FullName,
                            TransactionDateTime = jt.TransactionDateTime,
                            TransactionType = jt.TransactionType,
                            TransactionStatus = jt.TransactionStatus,
                            DebitAmount = jt.DebitAmount,
                            CreditAmount = jt.CreditAmount,
                            Currency = jt.Currency,
                            HoldAmount = jt.HoldAmount,
                            LastModified = jt.LastModified,
                            ParentTransactionID = jt.ParentTransactionID,
                            ClientReferenceNumber = jt.ClientReferenceNumber,
                            ScheduledTransactionID = jt.ScheduledTransactionID,
                            WalletID = jt.WalletID,
                            WalletName1 = jt.WalletName1,
                            WalletName2 = jt.WalletName2,
                            ClientAccountID = jt.ClientAccountID,
                            TransactionErrorCode = jt.TransactionErrorCode,
                            TransactionFailureReason = jt.TransactionFailureReason,
                            TransactionFlag = jt.TransactionFlag,
                            ELinxRequestID = jt.ELinxRequestID,
                            IsRefunded = jt.IsRefunded,
                            IsRefund = jt.IsRefund,
                            Notes = jt.Notes,
                            ChildTransactionIDs = jt.ChildTransactionIDs,
                            // Par défaut, IsPrelevementDone/IsNSFDone= false, IsException= false
                        };

                        // 8) Insérer en base
                        TransactionRepository.InsertOrUpdateTransaction(localTx);
                    }
                }
                // Sinon, ignorer les transactions qui ne sont pas "EFT Funding"
            }
        }
    }

    // Classes JSON
    public class RootObject
    {
        public List<JsonTransaction> Transactions { get; set; }
    }

    public class JsonTransaction
    {
        public string TransactionID { get; set; }
        public string AccountName { get; set; }
        public string TransactionDateTime { get; set; }
        public string TransactionType { get; set; }
        public string TransactionStatus { get; set; }
        public string Notes { get; set; }
        public string DebitAmount { get; set; }
        public string CreditAmount { get; set; }
        public string Currency { get; set; }
        public string HoldAmount { get; set; }
        public string LastModified { get; set; }
        public string ParentTransactionID { get; set; }
        public List<string> ChildTransactionIDs { get; set; }
        public string ClientReferenceNumber { get; set; }
        public string ScheduledTransactionID { get; set; }
        public string WalletID { get; set; }
        public string WalletName1 { get; set; }
        public string WalletName2 { get; set; }
        public string ClientAccountID { get; set; }
        public string TransactionErrorCode { get; set; }
        public string TransactionFailureReason { get; set; }
        public string TransactionFlag { get; set; }
        public string ELinxRequestID { get; set; }
        public bool IsRefunded { get; set; }
        public bool IsRefund { get; set; }
        public string FullName { get; set; }
    }
}


