using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInfluencerCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Influencers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InfluencerCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfluencerCategories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "InfluencerCategories",
                columns: new[] { "Id", "CreatedAt", "Name", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("6f1c2a10-0b5e-4d39-9a51-1c0e7a3d5001"), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Utc), "MP", 1 },
                    { new Guid("6f1c2a10-0b5e-4d39-9a51-1c0e7a3d5002"), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Utc), "Island Council", 2 },
                    { new Guid("6f1c2a10-0b5e-4d39-9a51-1c0e7a3d5003"), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Utc), "GM Member", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Influencers_CategoryId",
                table: "Influencers",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InfluencerCategories_Name",
                table: "InfluencerCategories",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Influencers_InfluencerCategories_CategoryId",
                table: "Influencers",
                column: "CategoryId",
                principalTable: "InfluencerCategories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Influencers_InfluencerCategories_CategoryId",
                table: "Influencers");

            migrationBuilder.DropTable(
                name: "InfluencerCategories");

            migrationBuilder.DropIndex(
                name: "IX_Influencers_CategoryId",
                table: "Influencers");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Influencers");
        }
    }
}
