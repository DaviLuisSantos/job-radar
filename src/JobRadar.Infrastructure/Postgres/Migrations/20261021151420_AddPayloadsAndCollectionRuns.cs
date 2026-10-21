using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JobRadar.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPayloadsAndCollectionRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collection_runs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    search_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    posted_within_days = table.Column<int>(type: "integer", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    found = table.Column<int>(type: "integer", nullable: false),
                    @new = table.Column<int>(name: "new", type: "integer", nullable: false),
                    complete = table.Column<bool>(type: "boolean", nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "listing_payloads",
                columns: table => new
                {
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_payloads", x => new { x.source, x.source_id, x.payload_hash });
                    table.ForeignKey(
                        name: "FK_listing_payloads_listings_source_source_id",
                        columns: x => new { x.source, x.source_id },
                        principalTable: "listings",
                        principalColumns: new[] { "source", "source_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_collection_runs_search",
                table: "collection_runs",
                columns: new[] { "source", "search_key", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_runs");

            migrationBuilder.DropTable(
                name: "listing_payloads");
        }
    }
}
