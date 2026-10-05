using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoterPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VoterPhotos",
                columns: table => new
                {
                    VoterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SizeBytes = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoterPhotos", x => x.VoterId);
                    table.ForeignKey(
                        name: "FK_VoterPhotos_Voters_VoterId",
                        column: x => x.VoterId,
                        principalTable: "Voters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Stored photos are lost; restore the backup taken before the upgrade if needed.
            migrationBuilder.Sql(
                "DELETE FROM RolePermissions WHERE Permission IN ('Voters.Photo.View', 'Voters.Photo.Edit'); " +
                "DELETE FROM UserPermissions WHERE Permission IN ('Voters.Photo.View', 'Voters.Photo.Edit');");

            migrationBuilder.DropTable(
                name: "VoterPhotos");
        }
    }
}
