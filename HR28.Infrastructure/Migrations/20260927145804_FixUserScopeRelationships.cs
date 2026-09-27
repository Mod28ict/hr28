using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUserScopeRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserScopes_Users_UserId1",
                table: "UserScopes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserScopes",
                table: "UserScopes");

            migrationBuilder.DropIndex(
                name: "IX_UserScopes_UserId1",
                table: "UserScopes");

            migrationBuilder.DropColumn(
                name: "ScopeLevel",
                table: "UserScopes");

            migrationBuilder.RenameColumn(
                name: "UserId1",
                table: "UserScopes",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserScopes",
                table: "UserScopes",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_UserScopes_UserId",
                table: "UserScopes",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserScopes_Users_UserId",
                table: "UserScopes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserScopes_Users_UserId",
                table: "UserScopes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserScopes",
                table: "UserScopes");

            migrationBuilder.DropIndex(
                name: "IX_UserScopes_UserId",
                table: "UserScopes");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "UserScopes",
                newName: "UserId1");

            migrationBuilder.AddColumn<int>(
                name: "ScopeLevel",
                table: "UserScopes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserScopes",
                table: "UserScopes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserScopes_UserId1",
                table: "UserScopes",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_UserScopes_Users_UserId1",
                table: "UserScopes",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
