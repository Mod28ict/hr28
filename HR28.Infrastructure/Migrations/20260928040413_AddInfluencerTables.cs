using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInfluencerTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Encounter_Users_RecordedByUserId",
                table: "Encounter");

            migrationBuilder.DropForeignKey(
                name: "FK_Encounter_Voters_VoterId",
                table: "Encounter");

            migrationBuilder.DropForeignKey(
                name: "FK_Influencer_Constituencies_ConstituencyId",
                table: "Influencer");

            migrationBuilder.DropForeignKey(
                name: "FK_Influencer_Islands_IslandId",
                table: "Influencer");

            migrationBuilder.DropForeignKey(
                name: "FK_VoterInfluencer_Influencer_InfluencerId",
                table: "VoterInfluencer");

            migrationBuilder.DropForeignKey(
                name: "FK_VoterInfluencer_Voters_VoterId",
                table: "VoterInfluencer");

            migrationBuilder.DropTable(
                name: "Pledge");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VoterInfluencer",
                table: "VoterInfluencer");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Influencer",
                table: "Influencer");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Encounter",
                table: "Encounter");

            migrationBuilder.RenameTable(
                name: "VoterInfluencer",
                newName: "VoterInfluencers");

            migrationBuilder.RenameTable(
                name: "Influencer",
                newName: "Influencers");

            migrationBuilder.RenameTable(
                name: "Encounter",
                newName: "Encounters");

            migrationBuilder.RenameIndex(
                name: "IX_VoterInfluencer_VoterId",
                table: "VoterInfluencers",
                newName: "IX_VoterInfluencers_VoterId");

            migrationBuilder.RenameIndex(
                name: "IX_VoterInfluencer_InfluencerId",
                table: "VoterInfluencers",
                newName: "IX_VoterInfluencers_InfluencerId");

            migrationBuilder.RenameIndex(
                name: "IX_Influencer_IslandId",
                table: "Influencers",
                newName: "IX_Influencers_IslandId");

            migrationBuilder.RenameIndex(
                name: "IX_Influencer_ConstituencyId",
                table: "Influencers",
                newName: "IX_Influencers_ConstituencyId");

            migrationBuilder.RenameIndex(
                name: "IX_Encounter_VoterId",
                table: "Encounters",
                newName: "IX_Encounters_VoterId");

            migrationBuilder.RenameIndex(
                name: "IX_Encounter_RecordedByUserId",
                table: "Encounters",
                newName: "IX_Encounters_RecordedByUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VoterInfluencers",
                table: "VoterInfluencers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Influencers",
                table: "Influencers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Encounters",
                table: "Encounters",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Pledges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PledgeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfilledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pledges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pledges_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Pledges_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Pledges_Voters_VoterId",
                        column: x => x.VoterId,
                        principalTable: "Voters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pledges_AssignedToUserId",
                table: "Pledges",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pledges_CreatedByUserId",
                table: "Pledges",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pledges_VoterId",
                table: "Pledges",
                column: "VoterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Encounters_Users_RecordedByUserId",
                table: "Encounters",
                column: "RecordedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Encounters_Voters_VoterId",
                table: "Encounters",
                column: "VoterId",
                principalTable: "Voters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Influencers_Constituencies_ConstituencyId",
                table: "Influencers",
                column: "ConstituencyId",
                principalTable: "Constituencies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Influencers_Islands_IslandId",
                table: "Influencers",
                column: "IslandId",
                principalTable: "Islands",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VoterInfluencers_Influencers_InfluencerId",
                table: "VoterInfluencers",
                column: "InfluencerId",
                principalTable: "Influencers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VoterInfluencers_Voters_VoterId",
                table: "VoterInfluencers",
                column: "VoterId",
                principalTable: "Voters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Encounters_Users_RecordedByUserId",
                table: "Encounters");

            migrationBuilder.DropForeignKey(
                name: "FK_Encounters_Voters_VoterId",
                table: "Encounters");

            migrationBuilder.DropForeignKey(
                name: "FK_Influencers_Constituencies_ConstituencyId",
                table: "Influencers");

            migrationBuilder.DropForeignKey(
                name: "FK_Influencers_Islands_IslandId",
                table: "Influencers");

            migrationBuilder.DropForeignKey(
                name: "FK_VoterInfluencers_Influencers_InfluencerId",
                table: "VoterInfluencers");

            migrationBuilder.DropForeignKey(
                name: "FK_VoterInfluencers_Voters_VoterId",
                table: "VoterInfluencers");

            migrationBuilder.DropTable(
                name: "Pledges");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VoterInfluencers",
                table: "VoterInfluencers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Influencers",
                table: "Influencers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Encounters",
                table: "Encounters");

            migrationBuilder.RenameTable(
                name: "VoterInfluencers",
                newName: "VoterInfluencer");

            migrationBuilder.RenameTable(
                name: "Influencers",
                newName: "Influencer");

            migrationBuilder.RenameTable(
                name: "Encounters",
                newName: "Encounter");

            migrationBuilder.RenameIndex(
                name: "IX_VoterInfluencers_VoterId",
                table: "VoterInfluencer",
                newName: "IX_VoterInfluencer_VoterId");

            migrationBuilder.RenameIndex(
                name: "IX_VoterInfluencers_InfluencerId",
                table: "VoterInfluencer",
                newName: "IX_VoterInfluencer_InfluencerId");

            migrationBuilder.RenameIndex(
                name: "IX_Influencers_IslandId",
                table: "Influencer",
                newName: "IX_Influencer_IslandId");

            migrationBuilder.RenameIndex(
                name: "IX_Influencers_ConstituencyId",
                table: "Influencer",
                newName: "IX_Influencer_ConstituencyId");

            migrationBuilder.RenameIndex(
                name: "IX_Encounters_VoterId",
                table: "Encounter",
                newName: "IX_Encounter_VoterId");

            migrationBuilder.RenameIndex(
                name: "IX_Encounters_RecordedByUserId",
                table: "Encounter",
                newName: "IX_Encounter_RecordedByUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VoterInfluencer",
                table: "VoterInfluencer",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Influencer",
                table: "Influencer",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Encounter",
                table: "Encounter",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Pledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PledgeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PledgeType = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pledge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pledge_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Pledge_Voters_VoterId",
                        column: x => x.VoterId,
                        principalTable: "Voters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pledge_RecordedByUserId",
                table: "Pledge",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pledge_VoterId",
                table: "Pledge",
                column: "VoterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Encounter_Users_RecordedByUserId",
                table: "Encounter",
                column: "RecordedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Encounter_Voters_VoterId",
                table: "Encounter",
                column: "VoterId",
                principalTable: "Voters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Influencer_Constituencies_ConstituencyId",
                table: "Influencer",
                column: "ConstituencyId",
                principalTable: "Constituencies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Influencer_Islands_IslandId",
                table: "Influencer",
                column: "IslandId",
                principalTable: "Islands",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VoterInfluencer_Influencer_InfluencerId",
                table: "VoterInfluencer",
                column: "InfluencerId",
                principalTable: "Influencer",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VoterInfluencer_Voters_VoterId",
                table: "VoterInfluencer",
                column: "VoterId",
                principalTable: "Voters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
