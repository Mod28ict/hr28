-- Run on HR28Train right after it is restored from a live backup (hr28-train-refresh).
-- Removes everything tied to live sign-ins and gives the training login (only) access.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;
    DELETE FROM OtpRequests;      -- codes sent by the live site
    DELETE FROM TrustedDevices;   -- live "Remember me" browsers (cookies are per site anyway)
    DELETE FROM KnownDevices;     -- sign-in alerts start fresh on training
COMMIT;

-- The live app login must not reach the training copy; the training login gets read/write here only.
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'hr28_app')
    DROP USER hr28_app;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'hr28_train')
    CREATE USER hr28_train FOR LOGIN hr28_train;

ALTER ROLE db_datareader ADD MEMBER hr28_train;
ALTER ROLE db_datawriter ADD MEMBER hr28_train;

SELECT CONCAT(N'Training copy: ', (SELECT COUNT(*) FROM Users), N' users, ',
              (SELECT COUNT(*) FROM Voters), N' voters, ',
              (SELECT COUNT(*) FROM Influencers), N' influencers, ',
              (SELECT COUNT(*) FROM Pledges), N' pledges.');
