using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddListingTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "listing_terms",
                columns: table => new
                {
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    term = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    found_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_terms", x => new { x.source, x.source_id, x.term });
                    table.ForeignKey(
                        name: "FK_listing_terms_listings_source_source_id",
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
                name: "listing_terms");
        }
    }
}
