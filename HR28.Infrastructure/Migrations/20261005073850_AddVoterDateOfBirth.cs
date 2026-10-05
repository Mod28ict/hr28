using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoterDateOfBirth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Voters",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Voters");
        }
    }
}
