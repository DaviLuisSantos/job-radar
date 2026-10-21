using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchHitsAndScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "listing_scores",
                columns: table => new
                {
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    profile = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    explanation = table.Column<string>(type: "jsonb", nullable: false),
                    rules_hash = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scored_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_scores", x => new { x.source, x.source_id, x.profile });
                    table.ForeignKey(
                        name: "FK_listing_scores_listings_source_source_id",
                        columns: x => new { x.source, x.source_id },
                        principalTable: "listings",
                        principalColumns: new[] { "source", "source_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listing_search_hits",
                columns: table => new
                {
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    search_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_search_hits", x => new { x.source, x.source_id, x.search_key });
                    table.ForeignKey(
                        name: "FK_listing_search_hits_listings_source_source_id",
                        columns: x => new { x.source, x.source_id },
                        principalTable: "listings",
                        principalColumns: new[] { "source", "source_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_listing_scores_profile_score",
                table: "listing_scores",
                columns: new[] { "profile", "score" });

            migrationBuilder.CreateIndex(
                name: "ix_listing_search_hits_search_key",
                table: "listing_search_hits",
                column: "search_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listing_scores");

            migrationBuilder.DropTable(
                name: "listing_search_hits");
        }
    }
}
