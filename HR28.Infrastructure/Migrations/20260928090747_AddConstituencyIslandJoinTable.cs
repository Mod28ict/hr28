using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConstituencyIslandJoinTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstituencyIslands",
                columns: table => new
                {
                    ConstituencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IslandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstituencyIslands", x => new { x.ConstituencyId, x.IslandId });
                    table.ForeignKey(
                        name: "FK_ConstituencyIslands_Constituencies_ConstituencyId",
                        column: x => x.ConstituencyId,
                        principalTable: "Constituencies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConstituencyIslands_Islands_IslandId",
                        column: x => x.IslandId,
                        principalTable: "Islands",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConstituencyIslands_IslandId",
                table: "ConstituencyIslands",
                column: "IslandId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstituencyIslands");
        }
    }
}
