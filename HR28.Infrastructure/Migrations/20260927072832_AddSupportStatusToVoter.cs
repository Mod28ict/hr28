using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportStatusToVoter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupportStatus",
                table: "Voters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportStatus",
                table: "Voters");
        }
    }
}
