using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMajlisImportFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NationalId",
                table: "Voters",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AtollCode",
                table: "Voters",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConstituencyCode",
                table: "Voters",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConstituencyName",
                table: "Voters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Voters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RegisteredIsland",
                table: "Voters",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ward",
                table: "Voters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Constituencies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Voters_AtollCode",
                table: "Voters",
                column: "AtollCode");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_ConstituencyCode",
                table: "Voters",
                column: "ConstituencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_Voters_NationalId",
                table: "Voters",
                column: "NationalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Voters_RegisteredIsland",
                table: "Voters",
                column: "RegisteredIsland");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Voters_AtollCode",
                table: "Voters");

            migrationBuilder.DropIndex(
                name: "IX_Voters_ConstituencyCode",
                table: "Voters");

            migrationBuilder.DropIndex(
                name: "IX_Voters_NationalId",
                table: "Voters");

            migrationBuilder.DropIndex(
                name: "IX_Voters_RegisteredIsland",
                table: "Voters");

            migrationBuilder.DropColumn(
                name: "AtollCode",
                table: "Voters");

            migrationBuilder.DropColumn(
                name: "ConstituencyCode",
                table: "Voters");

            migrationBuilder.DropColumn(
                name: "ConstituencyName",
                table: "Voters");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Voters");

            migrationBuilder.DropColumn(
                name: "RegisteredIsland",
                table: "Voters");

            migrationBuilder.DropColumn(
                name: "Ward",
                table: "Voters");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Constituencies");

            migrationBuilder.AlterColumn<string>(
                name: "NationalId",
                table: "Voters",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
