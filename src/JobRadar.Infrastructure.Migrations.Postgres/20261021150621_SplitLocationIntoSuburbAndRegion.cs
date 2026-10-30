using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class SplitLocationIntoSuburbAndRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old "city" held the metro/regional area ("Perth"), which is now "region".
            migrationBuilder.RenameColumn(
                name: "city",
                table: "listings",
                newName: "region");

            migrationBuilder.AddColumn<string>(
                name: "suburb",
                table: "listings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "suburb",
                table: "listings");

            migrationBuilder.RenameColumn(
                name: "region",
                table: "listings",
                newName: "city");
        }
    }
}
