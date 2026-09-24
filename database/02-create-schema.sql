USE [CashMovements];
GO

IF OBJECT_ID(N'dbo.CashMovements', N'U') IS NOT NULL
    DROP TABLE dbo.CashMovements;
GO

IF OBJECT_ID(N'dbo.Accounts', N'U') IS NOT NULL
    DROP TABLE dbo.Accounts;
GO

CREATE TABLE dbo.Accounts
(
    AccountId varchar(50) NOT NULL
        CONSTRAINT PK_Accounts PRIMARY KEY,
    Currency char(3) NOT NULL,
    Balance decimal(19,2) NOT NULL
        CONSTRAINT DF_Accounts_Balance DEFAULT (0),
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_Accounts_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

    CONSTRAINT CK_Accounts_Currency CHECK (LEN(Currency) = 3)
);
GO

CREATE TABLE dbo.CashMovements
(
    Id uniqueidentifier NOT NULL
        CONSTRAINT PK_CashMovements PRIMARY KEY,
    AccountId varchar(50) NOT NULL,
    ExternalRef varchar(100) NOT NULL,
    Currency char(3) NOT NULL,
    Amount decimal(19,2) NOT NULL,
    OccurredAtUtc datetime2(7) NOT NULL,
    Narration nvarchar(500) NOT NULL,
    CreatedAtUtc datetime2(7) NOT NULL
        CONSTRAINT DF_CashMovements_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_CashMovements_Accounts
        FOREIGN KEY (AccountId) REFERENCES dbo.Accounts(AccountId),
    CONSTRAINT UQ_CashMovements_Account_ExternalRef
        UNIQUE (AccountId, ExternalRef),
    CONSTRAINT CK_CashMovements_Amount_NotZero CHECK (Amount <> 0),
    CONSTRAINT CK_CashMovements_Currency CHECK (LEN(Currency) = 3)
);
GO

CREATE INDEX IX_CashMovements_Account_OccurredAt
ON dbo.CashMovements(AccountId, OccurredAtUtc, Id)
INCLUDE (ExternalRef, Currency, Amount, Narration, CreatedAtUtc);
GO
