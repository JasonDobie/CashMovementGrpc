USE [CashMovements];
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE AccountId = 'ACC-001')
    INSERT INTO dbo.Accounts(AccountId, Currency, Balance) VALUES ('ACC-001', 'ZAR', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE AccountId = 'ACC-002')
    INSERT INTO dbo.Accounts(AccountId, Currency, Balance) VALUES ('ACC-002', 'ZAR', 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE AccountId = 'ACC-003')
    INSERT INTO dbo.Accounts(AccountId, Currency, Balance) VALUES ('ACC-003', 'USD', 0);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.CashMovements WHERE AccountId='ACC-001' AND ExternalRef='MOV-20240715-000123')
BEGIN
    INSERT INTO dbo.CashMovements
        (Id, AccountId, ExternalRef, Currency, Amount, OccurredAtUtc, Narration)
    VALUES
        (NEWID(), 'ACC-001', 'MOV-20240715-000123', 'ZAR', 12500.00, '2024-07-15T10:42:31', N'Initial deposit'),
        (NEWID(), 'ACC-001', 'MOV-20240715-000124', 'ZAR', -1500.00, '2024-07-16T12:10:00', N'Cash withdrawal'),
        (NEWID(), 'ACC-001', 'MOV-20240717-000125', 'ZAR', 2500.00, '2024-07-17T09:30:00', N'Counter deposit');

    UPDATE dbo.Accounts
    SET Balance = Balance + 13500.00
    WHERE AccountId = 'ACC-001';
END
GO
