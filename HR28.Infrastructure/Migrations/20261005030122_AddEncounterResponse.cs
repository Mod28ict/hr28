using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HR28.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEncounterResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Response",
                table: "Encounters",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Owner decision 2026-10-05: outcomes become Meet / Call / Request and the
            // voter's response (green / yellow / red) is stored separately.
            // The old outcome described the response; the type tells meeting from call.
            migrationBuilder.Sql("""
                UPDATE Encounters SET Response =
                    CASE Outcome
                        WHEN 'Positive' THEN 'Supports'
                        WHEN 'Negative' THEN 'Does not support'
                        WHEN 'Undecided' THEN 'Undecided'
                        WHEN 'Follow-up Required' THEN 'Undecided'
                        ELSE NULL
                    END
                WHERE Outcome NOT IN ('Meet', 'Call', 'Request');

                UPDATE Encounters SET Outcome =
                    CASE WHEN EncounterType = 'Phone Call' THEN 'Call' ELSE 'Meet' END
                WHERE Outcome NOT IN ('Meet', 'Call', 'Request');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to the old outcome list. "Follow-up Required" comes back as
            // "Undecided" (restore the backup for the exact original values).
            migrationBuilder.Sql("""
                UPDATE Encounters SET Outcome =
                    CASE Response
                        WHEN 'Supports' THEN 'Positive'
                        WHEN 'Does not support' THEN 'Negative'
                        WHEN 'Undecided' THEN 'Undecided'
                        ELSE 'No Contact'
                    END;
                """);

            migrationBuilder.DropColumn(
                name: "Response",
                table: "Encounters");
        }
    }
}
