using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPoliticalParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PoliticalPartyId",
                table: "Voters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PoliticalParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDefaultFilter = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticalParties", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PoliticalParties",
                columns: new[] { "Id", "CreatedAt", "IsDefaultFilter", "Name", "ShortName", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("8a3e5c20-4f1d-4b7a-9c2e-5d0f6b1a7001"), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Utc), true, "Maldivian Democratic Party", "MDP", 1 },
                    { new Guid("8a3e5c20-4f1d-4b7a-9c2e-5d0f6b1a7002"), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Utc), false, "People's National Congress", "PNC", 2 },
                    { new Guid("8a3e5c20-4f1d-4b7a-9c2e-5d0f6b1a7003"), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Utc), false, "Jumhooree Party", "JP", 3 },
                    { new Guid("8a3e5c20-4f1d-4b7a-9c2e-5d0f6b1a7004"), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Utc), false, "Maldives Development Alliance", "MDA", 4 },
                    { new Guid("8a3e5c20-4f1d-4b7a-9c2e-5d0f6b1a7005"), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Utc), false, "Adhaalath Party", "AP", 5 },
                    { new Guid("8a3e5c20-4f1d-4b7a-9c2e-5d0f6b1a7006"), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Utc), false, "Maldives National Party", "MNP", 6 },
                    { new Guid("8a3e5c20-4f1d-4b7a-9c2e-5d0f6b1a7007"), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Utc), false, "People's National Front", "PNF", 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Voters_PoliticalPartyId",
                table: "Voters",
                column: "PoliticalPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_PoliticalParties_Name",
                table: "PoliticalParties",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Voters_PoliticalParties_PoliticalPartyId",
                table: "Voters",
                column: "PoliticalPartyId",
                principalTable: "PoliticalParties",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Voters_PoliticalParties_PoliticalPartyId",
                table: "Voters");

            migrationBuilder.DropTable(
                name: "PoliticalParties");

            migrationBuilder.DropIndex(
                name: "IX_Voters_PoliticalPartyId",
                table: "Voters");

            migrationBuilder.DropColumn(
                name: "PoliticalPartyId",
                table: "Voters");
        }
    }
}
