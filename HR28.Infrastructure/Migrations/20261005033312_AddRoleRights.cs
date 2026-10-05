using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleRights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VoterProfileView",
                table: "Roles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Full");

            // Rights replace the old "every role except Reporter may write" rule. The
            // built-in roles get exactly what they could do before, so nobody gains or
            // loses access on upgrade. (Influencers.Edit/Delete and Encounters.Edit stay
            // granted rights; the Administrator always has everything.)
            migrationBuilder.Sql("""
                INSERT INTO RolePermissions (RoleId, Permission)
                SELECT r.Id, p.Permission
                FROM Roles r
                CROSS JOIN (VALUES
                    ('Voters.View'), ('Voters.Add'), ('Voters.Edit'),
                    ('Encounters.View'), ('Encounters.Add'),
                    ('Pledges.View'), ('Pledges.Add'), ('Pledges.Edit'),
                    ('Influencers.View'), ('Influencers.Add'), ('Influencers.Link')
                ) AS p(Permission)
                WHERE r.Name IN ('National Administrator', 'Constituency Administrator', 'Island Administrator', 'Collector')
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.Permission = p.Permission);

                -- Deleting voters was for Super and National Administrators only.
                INSERT INTO RolePermissions (RoleId, Permission)
                SELECT r.Id, 'Voters.Delete'
                FROM Roles r
                WHERE r.Name = 'National Administrator'
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.Permission = 'Voters.Delete');

                -- Reporters only read reports; their menu also showed Encounters and Pledges
                -- (not Voters or Influencers), so that is what they keep.
                INSERT INTO RolePermissions (RoleId, Permission)
                SELECT r.Id, p.Permission
                FROM Roles r
                CROSS JOIN (VALUES
                    ('Encounters.View'), ('Pledges.View')
                ) AS p(Permission)
                WHERE r.Name = 'Reporter'
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.Permission = p.Permission);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removes the rights this version introduced (the earlier three stay).
            // Custom roles stay as plain roles.
            migrationBuilder.Sql("""
                DELETE FROM RolePermissions WHERE Permission IN (
                    'Voters.View', 'Voters.Add', 'Voters.Edit', 'Voters.Delete',
                    'Encounters.View', 'Encounters.Add', 'Encounters.Delete',
                    'Pledges.View', 'Pledges.Add', 'Pledges.Edit', 'Pledges.Delete',
                    'Influencers.View', 'Influencers.Add', 'Influencers.Link');

                DELETE FROM UserPermissions WHERE Permission IN (
                    'Voters.View', 'Voters.Add', 'Voters.Edit', 'Voters.Delete',
                    'Encounters.View', 'Encounters.Add', 'Encounters.Delete',
                    'Pledges.View', 'Pledges.Add', 'Pledges.Edit', 'Pledges.Delete',
                    'Influencers.View', 'Influencers.Add', 'Influencers.Link');
                """);

            migrationBuilder.DropColumn(
                name: "VoterProfileView",
                table: "Roles");
        }
    }
}
