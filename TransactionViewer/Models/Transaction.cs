using System;
using System.Collections.Generic;

namespace TransactionViewer.Models
{
    public class Transaction
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

        // Indicateurs
        public bool IsPrelevementDone { get; set; }
        public bool IsNSFDone { get; set; }

        // Indique si la transaction est en statut "Exception"
        public bool IsException { get; set; }

        // NOUVEAU : Indique si la transaction a été vérifiée manuellement
        public bool IsVerifier { get; set; }
    }
}


