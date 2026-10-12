-- Production only, ONCE, after copying the development database:
-- removes the temporary test accounts and the sign-in data that came with the copy.
-- Codes and remembered devices from development are useless under the new production key.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @test TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @test SELECT Id FROM Users
WHERE Id IN ('C1A0DE00-0000-4000-8000-000000000001', 'C1A0DE00-0000-4000-8000-000000000002');

DELETE FROM UserPermissions WHERE UserId IN (SELECT Id FROM @test);
DELETE FROM UserScopes WHERE UserId IN (SELECT Id FROM @test);
DELETE FROM UserRoles WHERE UserId IN (SELECT Id FROM @test);
DELETE FROM AuthorizationCodeHistories WHERE UserId IN (SELECT Id FROM @test);
DELETE FROM OtpRequests;
DELETE FROM TrustedDevices;
DELETE FROM Users WHERE Id IN (SELECT Id FROM @test);

COMMIT;
SELECT 'Test accounts removed: ' + CAST((SELECT COUNT(*) FROM @test) AS varchar(10));
