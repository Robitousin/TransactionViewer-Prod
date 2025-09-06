/* Création base (adapter chemins si besoin) */
IF DB_ID('TransactionsDB') IS NULL
BEGIN
    CREATE DATABASE TransactionsDB;
END
GO
USE TransactionsDB;
GO

/* Table Transactions */
IF OBJECT_ID('dbo.Transactions','U') IS NOT NULL
    DROP TABLE dbo.Transactions;
GO

CREATE TABLE dbo.Transactions
(
    TransactionID             NVARCHAR(50)  NOT NULL PRIMARY KEY,
    FullName                  NVARCHAR(150) NULL,
    AccountName               NVARCHAR(100) NULL,
    TransactionDateTime       DATETIME      NULL,
    TransactionType           NVARCHAR(50)  NULL,
    TransactionStatus         NVARCHAR(40)  NULL,
    Notes                     NVARCHAR(MAX) NULL,
    DebitAmount               DECIMAL(18,2) NULL,
    CreditAmount              DECIMAL(18,2) NULL,
    Currency                  NVARCHAR(10)  NULL,
    HoldAmount                DECIMAL(18,2) NULL,
    LastModified              DATETIME      NULL,
    ParentTransactionID       NVARCHAR(50)  NULL,
    ChildTransactionIDs       NVARCHAR(MAX) NULL, -- JSON / CSV
    ClientReferenceNumber     NVARCHAR(60)  NULL,
    ScheduledTransactionID    NVARCHAR(60)  NULL,
    WalletID                  NVARCHAR(60)  NULL,
    WalletName1               NVARCHAR(120) NULL,
    WalletName2               NVARCHAR(120) NULL,
    ClientAccountID           NVARCHAR(60)  NULL,
    TransactionErrorCode      NVARCHAR(40)  NULL,
    TransactionFailureReason  NVARCHAR(400) NULL,
    TransactionFlag           NVARCHAR(60)  NULL,
    ELinxRequestID            NVARCHAR(60)  NULL,
    IsRefunded                BIT NOT NULL CONSTRAINT DF_Transactions_IsRefunded DEFAULT(0),
    IsRefund                  BIT NOT NULL CONSTRAINT DF_Transactions_IsRefund DEFAULT(0),
    IsPrelevementDone         BIT NOT NULL CONSTRAINT DF_Transactions_IsPrelevementDone DEFAULT(0),
    IsNSFDone                 BIT NOT NULL CONSTRAINT DF_Transactions_IsNSFDone DEFAULT(0),
    IsException               BIT NOT NULL CONSTRAINT DF_Transactions_IsException DEFAULT(0),
    IsVerifier                BIT NOT NULL CONSTRAINT DF_Transactions_IsVerifier DEFAULT(0)
);
GO

/* Index selon filtres / tris utilisés */
CREATE INDEX IX_Transactions_Status          ON dbo.Transactions(TransactionStatus);
CREATE INDEX IX_Transactions_IsException     ON dbo.Transactions(IsException) INCLUDE(LastModified);
CREATE INDEX IX_Transactions_ProcessFlags    ON dbo.Transactions(IsPrelevementDone, IsNSFDone);
CREATE INDEX IX_Transactions_LastModified    ON dbo.Transactions(LastModified);
CREATE INDEX IX_Transactions_CreditAmount    ON dbo.Transactions(CreditAmount);
GO

/* Exemple: vue simple pour Exceptions */
IF OBJECT_ID('dbo.vw_Exceptions','V') IS NOT NULL
    DROP VIEW dbo.vw_Exceptions;
GO
CREATE VIEW dbo.vw_Exceptions AS
SELECT *
FROM dbo.Transactions
WHERE IsException = 1 OR TransactionStatus = 'cancelled';
GO