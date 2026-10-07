using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <summary>
    /// Data only (owner decision, 2026-10-07): every role with "Edit voters" also gets the
    /// new "Change support status" right, so those roles keep changing statuses as before.
    /// Each role is audited. Rollback: dotnet ef database update AddRoleStartPage.
    /// </summary>
    public partial class GrantVoterStatusToEditors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE @granted TABLE (RoleId uniqueidentifier, RoleName nvarchar(256));

                INSERT INTO RolePermissions (RoleId, Permission)
                OUTPUT inserted.RoleId, NULL INTO @granted
                SELECT rp.RoleId, 'Voters.Status'
                FROM RolePermissions rp
                WHERE rp.Permission = 'Voters.Edit'
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions x
                                  WHERE x.RoleId = rp.RoleId AND x.Permission = 'Voters.Status');

                INSERT INTO AuditLogs (Id, UserId, Action, EntityName, EntityId, CreatedAt)
                SELECT NEWID(), NULL,
                       'Rights for role ""' + r.Name + '"": granted Change support status (update: roles with Edit voters)',
                       'Role', r.Name, SYSUTCDATETIME()
                FROM @granted g
                JOIN Roles r ON r.Id = g.RoleId;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE sp
                FROM RolePermissions sp
                WHERE sp.Permission = 'Voters.Status'
                  AND EXISTS (SELECT 1 FROM RolePermissions e
                              WHERE e.RoleId = sp.RoleId AND e.Permission = 'Voters.Edit');
            ");
        }
    }
}
