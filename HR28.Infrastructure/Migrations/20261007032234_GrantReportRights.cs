using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <summary>
    /// Data only (owner decision, 2026-10-07): reports become rights. Until now every
    /// signed-in user could open and download reports, so every existing role (except the
    /// Administrator, who always has every right) gets "View reports" and "Download and
    /// print reports"; the Administrator can then take them away. Each role is audited.
    /// Rollback: dotnet ef database update GrantVoterStatusToEditors.
    /// </summary>
    public partial class GrantReportRights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE @granted TABLE (RoleId uniqueidentifier);

                INSERT INTO RolePermissions (RoleId, Permission)
                OUTPUT inserted.RoleId INTO @granted
                SELECT r.Id, p.Permission
                FROM Roles r
                CROSS JOIN (VALUES ('Reports.View'), ('Reports.Download')) AS p(Permission)
                WHERE r.Name <> 'Super Administrator'
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions x
                                  WHERE x.RoleId = r.Id AND x.Permission = p.Permission);

                INSERT INTO AuditLogs (Id, UserId, Action, EntityName, EntityId, CreatedAt)
                SELECT NEWID(), NULL,
                       'Rights for role ""' + r.Name + '"": granted View reports, Download and print reports (update: reports became rights)',
                       'Role', r.Name, SYSUTCDATETIME()
                FROM Roles r
                WHERE r.Id IN (SELECT DISTINCT RoleId FROM @granted);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM RolePermissions WHERE Permission IN ('Reports.View', 'Reports.Download');
                DELETE FROM UserPermissions WHERE Permission IN ('Reports.View', 'Reports.Download');
            ");
        }
    }
}
