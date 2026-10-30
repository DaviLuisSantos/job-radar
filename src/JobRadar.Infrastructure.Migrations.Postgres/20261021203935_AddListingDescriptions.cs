using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddListingDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "listing_descriptions",
                columns: table => new
                {
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_descriptions", x => new { x.source, x.source_id });
                    table.ForeignKey(
                        name: "FK_listing_descriptions_listings_source_source_id",
                        columns: x => new { x.source, x.source_id },
                        principalTable: "listings",
                        principalColumns: new[] { "source", "source_id" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listing_descriptions");
        }
    }
}
