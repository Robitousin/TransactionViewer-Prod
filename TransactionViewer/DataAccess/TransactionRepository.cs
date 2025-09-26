using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using Newtonsoft.Json;
using TransactionViewer.Models;

namespace TransactionViewer.DataAccess
{
    public static class TransactionRepository
    {
        // =======================
        // = Onglet Prelevements =
        // =======================
        public static List<Transaction> GetPrelevements()
        {
            var list = new List<Transaction>();
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnString))
            {
                conn.Open();
                string sql = @"
                    SELECT *
                    FROM Transactions
                    WHERE IsPrelevementDone = 0   -- masquer prélevements déjà traités
                      AND IsException = 0        -- masquer s'ils sont en 'exception'
                      AND TransactionStatus <> 'cancelled'  -- exclure 'cancelled'
                    ORDER BY CreditAmount ASC
                ";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        list.Add(MapToTransaction(rdr));
                    }
                }
            }
            return list;
        }

        // ===============
        // = Onglet NSF =
        // ===============
        public static List<Transaction> GetNSF()
        {
            var list = new List<Transaction>();
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnString))
            {
                conn.Open();
                string sql = @"
                    SELECT *
                    FROM Transactions
                    WHERE TransactionStatus = 'failed'
                      AND TransactionStatus <> 'cancelled' -- exclure 'cancelled'
                      AND IsNSFDone = 0        -- masquer déjà traitées en NSF
                      AND IsException = 0      -- masquer s'ils sont en 'exception'
                    ORDER BY LastModified ASC
                ";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        list.Add(MapToTransaction(rdr));
                    }
                }
            }
            return list;
        }

        // =====================
        // = Onglet Exceptions =
        // =====================
        public static List<Transaction> GetExceptions()
        {
            var list = new List<Transaction>();
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnString))
            {
                conn.Open();
                // Exceptions = IsException=1 OU cancelled
                // MAIS on exclut explicitement le cas "ré-ouvert" ET déjà traité (Prélèvements ou NSF)
                string sql = @"
            SELECT *
            FROM Transactions
            WHERE
                (IsException = 1 OR TransactionStatus = 'cancelled')
                AND NOT (
                    TransactionStatus IN ('in progress', 'reopen')
                    AND (IsPrelevementDone = 1 OR IsNSFDone = 1)
                )
            ORDER BY LastModified ASC
        ";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        list.Add(MapToTransaction(rdr));
                    }
                }
            }
            return list;
        }


        // ==========================
        // = Mise à jour de statut =
        // ==========================
        public static void UpdatePrelevementDone(string id)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnString))
            {
                conn.Open();
                string sql = "UPDATE Transactions SET IsPrelevementDone=1 WHERE TransactionID=@id";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateNSFDone(string id)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnString))
            {
                conn.Open();
                string sql = "UPDATE Transactions SET IsNSFDone=1 WHERE TransactionID=@id";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ===============================
        // = InsertOrUpdate / Queries =
        // ===============================
        public static void InsertOrUpdateTransaction(Transaction tx)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnString))
            {
                conn.Open();
                string checkSql = "SELECT COUNT(*) FROM Transactions WHERE TransactionID=@tid";
                using (SqlCommand checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@tid", tx.TransactionID);
                    int count = (int)checkCmd.ExecuteScalar();
                    if (count > 0)
                        Update(tx, conn);
                    else
                        Insert(tx, conn);
                }
            }
        }

        public static Transaction GetByTransactionID(string tid)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnString))
            {
                conn.Open();
                string sql = "SELECT * FROM Transactions WHERE TransactionID=@tid";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@tid", tid);
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            return MapToTransaction(rdr);
                        }
                    }
                }
            }
            return null;
        }

        private static void Insert(Transaction tx, SqlConnection conn)
        {
            string sql = @"
                INSERT INTO Transactions
                (
                    TransactionID, FullName, AccountName, TransactionDateTime, TransactionType,
                    TransactionStatus, Notes, DebitAmount, CreditAmount, Currency, HoldAmount,
                    LastModified, ParentTransactionID, ChildTransactionIDs, ClientReferenceNumber,
                    ScheduledTransactionID, WalletID, WalletName1, WalletName2, ClientAccountID,
                    TransactionErrorCode, TransactionFailureReason, TransactionFlag, ELinxRequestID,
                    IsRefunded, IsRefund, IsPrelevementDone, IsNSFDone,
                    IsException,
                    -- NOUVEAU
                    IsVerifier
                )
                VALUES
                (
                    @TID, @FName, @AccName, @TDateTime, @TType,
                    @TStatus, @Notes, @DebitAmt, @CreditAmt, @Curr, @HoldAmt,
                    @LastMod, @ParentTID, @ChildTIDs, @ClientRef,
                    @SchedTID, @WID, @WName1, @WName2, @CliAccID,
                    @ErrCode, @FailReason, @TFlag, @ELinx,
                    @Refd, @ReF, 0, 0,
                    @IsExc,
                    @IsVer
                )
            ";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@TID", tx.TransactionID);
                cmd.Parameters.AddWithValue("@FName", (object)tx.FullName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@AccName", (object)tx.AccountName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TDateTime", ParseDateTime(tx.TransactionDateTime));
                cmd.Parameters.AddWithValue("@TType", tx.TransactionType ?? "");
                cmd.Parameters.AddWithValue("@TStatus", tx.TransactionStatus ?? "");
                cmd.Parameters.AddWithValue("@Notes", (object)tx.Notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DebitAmt", ParseDecimal(tx.DebitAmount));
                cmd.Parameters.AddWithValue("@CreditAmt", ParseDecimal(tx.CreditAmount));
                cmd.Parameters.AddWithValue("@Curr", (object)tx.Currency ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@HoldAmt", ParseDecimal(tx.HoldAmount));
                cmd.Parameters.AddWithValue("@LastMod", ParseDateTime(tx.LastModified));
                cmd.Parameters.AddWithValue("@ParentTID", (object)tx.ParentTransactionID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ChildTIDs", (object)SerializeChildIDs(tx.ChildTransactionIDs) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ClientRef", (object)tx.ClientReferenceNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SchedTID", (object)tx.ScheduledTransactionID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WID", (object)tx.WalletID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WName1", (object)tx.WalletName1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WName2", (object)tx.WalletName2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CliAccID", (object)tx.ClientAccountID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ErrCode", (object)tx.TransactionErrorCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FailReason", (object)tx.TransactionFailureReason ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TFlag", (object)tx.TransactionFlag ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ELinx", (object)tx.ELinxRequestID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Refd", tx.IsRefunded);
                cmd.Parameters.AddWithValue("@ReF", tx.IsRefund);

                cmd.Parameters.AddWithValue("@IsExc", tx.IsException);

                // NOUVEAU
                cmd.Parameters.AddWithValue("@IsVer", tx.IsVerifier);

                cmd.ExecuteNonQuery();
            }
        }

        private static void Update(Transaction tx, SqlConnection conn)
        {
            string sql = @"
                UPDATE Transactions
                SET FullName=@FName,
                    AccountName=@AccName,
                    TransactionDateTime=@TDateTime,
                    TransactionType=@TType,
                    TransactionStatus=@TStatus,
                    Notes=@Notes,
                    DebitAmount=@DebitAmt,
                    CreditAmount=@CreditAmt,
                    Currency=@Curr,
                    HoldAmount=@HoldAmt,
                    LastModified=@LastMod,
                    ParentTransactionID=@ParentTID,
                    ChildTransactionIDs=@ChildTIDs,
                    ClientReferenceNumber=@ClientRef,
                    ScheduledTransactionID=@SchedTID,
                    WalletID=@WID,
                    WalletName1=@WName1,
                    WalletName2=@WName2,
                    ClientAccountID=@CliAccID,
                    TransactionErrorCode=@ErrCode,
                    TransactionFailureReason=@FailReason,
                    TransactionFlag=@TFlag,
                    ELinxRequestID=@ELinx,
                    IsRefunded=@Refd,
                    IsRefund=@ReF,
                    IsException=@IsExc,
                    -- NOUVEAU
                    IsVerifier=@IsVer
                WHERE TransactionID=@TID
            ";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@TID", tx.TransactionID);
                cmd.Parameters.AddWithValue("@FName", (object)tx.FullName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@AccName", (object)tx.AccountName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TDateTime", ParseDateTime(tx.TransactionDateTime));
                cmd.Parameters.AddWithValue("@TType", tx.TransactionType ?? "");
                cmd.Parameters.AddWithValue("@TStatus", tx.TransactionStatus ?? "");
                cmd.Parameters.AddWithValue("@Notes", (object)tx.Notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DebitAmt", ParseDecimal(tx.DebitAmount));
                cmd.Parameters.AddWithValue("@CreditAmt", ParseDecimal(tx.CreditAmount));
                cmd.Parameters.AddWithValue("@Curr", (object)tx.Currency ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@HoldAmt", ParseDecimal(tx.HoldAmount));
                cmd.Parameters.AddWithValue("@LastMod", ParseDateTime(tx.LastModified));
                cmd.Parameters.AddWithValue("@ParentTID", (object)tx.ParentTransactionID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ChildTIDs", (object)SerializeChildIDs(tx.ChildTransactionIDs) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ClientRef", (object)tx.ClientReferenceNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SchedTID", (object)tx.ScheduledTransactionID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WID", (object)tx.WalletID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WName1", (object)tx.WalletName1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WName2", (object)tx.WalletName2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CliAccID", (object)tx.ClientAccountID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ErrCode", (object)tx.TransactionErrorCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FailReason", (object)tx.TransactionFailureReason ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TFlag", (object)tx.TransactionFlag ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ELinx", (object)tx.ELinxRequestID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Refd", tx.IsRefunded);
                cmd.Parameters.AddWithValue("@ReF", tx.IsRefund);
                cmd.Parameters.AddWithValue("@IsExc", tx.IsException);

                // NOUVEAU
                cmd.Parameters.AddWithValue("@IsVer", tx.IsVerifier);

                cmd.ExecuteNonQuery();
            }
        }

        private static Transaction MapToTransaction(SqlDataReader rdr)
        {
            var tx = new Transaction
            {
                TransactionID = (string)rdr["TransactionID"],
                FullName = rdr["FullName"] as string,
                AccountName = rdr["AccountName"] as string,
                TransactionType = (string)rdr["TransactionType"],
                TransactionStatus = (string)rdr["TransactionStatus"],
                Notes = rdr["Notes"] as string,
                DebitAmount = rdr["DebitAmount"]?.ToString(),
                CreditAmount = rdr["CreditAmount"]?.ToString(),
                Currency = rdr["Currency"] as string,
                HoldAmount = rdr["HoldAmount"]?.ToString(),
                ParentTransactionID = rdr["ParentTransactionID"] as string,
                ClientReferenceNumber = rdr["ClientReferenceNumber"] as string,
                ScheduledTransactionID = rdr["ScheduledTransactionID"] as string,
                WalletID = rdr["WalletID"] as string,
                WalletName1 = rdr["WalletName1"] as string,
                WalletName2 = rdr["WalletName2"] as string,
                ClientAccountID = rdr["ClientAccountID"] as string,
                TransactionErrorCode = rdr["TransactionErrorCode"] as string,
                TransactionFailureReason = rdr["TransactionFailureReason"] as string,
                TransactionFlag = rdr["TransactionFlag"] as string,
                ELinxRequestID = rdr["ELinxRequestID"] as string,
                IsRefunded = (bool)rdr["IsRefunded"],
                IsRefund = (bool)rdr["IsRefund"],
                IsPrelevementDone = (bool)rdr["IsPrelevementDone"],
                IsNSFDone = (bool)rdr["IsNSFDone"],
                IsException = (rdr["IsException"] != DBNull.Value) && (bool)rdr["IsException"],

                // NOUVEAU
                IsVerifier = false
            };

            // S'il existe en DB => remplir
            if (rdr.GetSchemaTable().Select("ColumnName='IsVerifier'").Length > 0)
            {
                if (!rdr.IsDBNull(rdr.GetOrdinal("IsVerifier")))
                {
                    tx.IsVerifier = (bool)rdr["IsVerifier"];
                }
            }

            // DateTimes => en string
            if (!rdr.IsDBNull(rdr.GetOrdinal("TransactionDateTime")))
            {
                var dt = (DateTime)rdr["TransactionDateTime"];
                tx.TransactionDateTime = dt.ToString("yyyy-MM-dd HH:mm:ss");
            }
            if (!rdr.IsDBNull(rdr.GetOrdinal("LastModified")))
            {
                var dt = (DateTime)rdr["LastModified"];
                tx.LastModified = dt.ToString("yyyy-MM-dd HH:mm:ss");
            }

            // ChildTransactionIDs
            if (!rdr.IsDBNull(rdr.GetOrdinal("ChildTransactionIDs")))
            {
                var str = (string)rdr["ChildTransactionIDs"];
                tx.ChildTransactionIDs = DeserializeChildIDs(str);
            }

            return tx;
        }

        // ========================
        // = Utils
        // ========================

        private static decimal ParseDecimal(string input)
        {
            if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d))
                return d;
            return 0;
        }

        private static DateTime? ParseDateTime(string input)
        {
            if (DateTime.TryParse(input, out DateTime dt))
                return dt;
            return null;
        }

        private static string SerializeChildIDs(List<string> list)
        {
            if (list == null || list.Count == 0) return null;
            return JsonConvert.SerializeObject(list);
        }

        private static List<string> DeserializeChildIDs(string str)
        {
            if (string.IsNullOrEmpty(str)) return new List<string>();
            try
            {
                return JsonConvert.DeserializeObject<List<string>>(str);
            }
            catch
            {
                return new List<string>(str.Split(','));
            }
        }
    }
}





