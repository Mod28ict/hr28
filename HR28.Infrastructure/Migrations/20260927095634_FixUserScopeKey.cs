using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUserScopeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserScopes_Users_UserId",
                table: "UserScopes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserScopes",
                table: "UserScopes");

            migrationBuilder.AlterColumn<Guid>(
                name: "IslandId",
                table: "UserScopes",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConstituencyId",
                table: "UserScopes",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "UserScopes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "UserId1",
                table: "UserScopes");

            migrationBuilder.AlterColumn<Guid>(
                name: "IslandId",
                table: "UserScopes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConstituencyId",
                table: "UserScopes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserScopes",
                table: "UserScopes",
                columns: new[] { "UserId", "ConstituencyId", "IslandId" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserScopes_Users_UserId",
                table: "UserScopes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
