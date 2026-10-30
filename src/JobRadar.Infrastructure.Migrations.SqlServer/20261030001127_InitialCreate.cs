using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_experience",
                columns: table => new
                {
                    profile = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    years = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_experience", x => x.profile);
                });

            migrationBuilder.CreateTable(
                name: "candidate_requirements",
                columns: table => new
                {
                    item_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    state = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_requirements", x => x.item_id);
                });

            migrationBuilder.CreateTable(
                name: "collection_runs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    search_key = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    posted_within_days = table.Column<int>(type: "int", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    found = table.Column<int>(type: "int", nullable: false),
                    @new = table.Column<int>(name: "new", type: "int", nullable: false),
                    complete = table.Column<bool>(type: "bit", nullable: false),
                    error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "listings",
                columns: table => new
                {
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    company = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    suburb = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    region = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    state = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    postcode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    work_type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    salary_text = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    posted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    source_category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    first_seen_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listings", x => new { x.source, x.source_id });
                });

            migrationBuilder.CreateTable(
                name: "market_listings",
                columns: table => new
                {
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    city = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    company = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    work_type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    salary_text = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    salary_period = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    salary_annual_min = table.Column<int>(type: "int", nullable: true),
                    salary_annual_max = table.Column<int>(type: "int", nullable: true),
                    posted_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    first_seen = table.Column<DateOnly>(type: "date", nullable: false),
                    last_seen = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_listings", x => new { x.source, x.source_id, x.category, x.city });
                });

            migrationBuilder.CreateTable(
                name: "market_snapshots",
                columns: table => new
                {
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    expected = table.Column<int>(type: "int", nullable: false),
                    saved = table.Column<int>(type: "int", nullable: false),
                    listings = table.Column<int>(type: "int", nullable: false),
                    issues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    taken_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_snapshots", x => new { x.source, x.snapshot_date });
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => new { x.source, x.source_id });
                    table.ForeignKey(
                        name: "FK_applications_listings_source_source_id",
                        columns: x => new { x.source, x.source_id },
                        principalTable: "listings",
                        principalColumns: new[] { "source", "source_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listing_descriptions",
                columns: table => new
                {
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    origin = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "listing_labels",
                columns: table => new
                {
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    verdict = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    labeled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listing_labels", x => new { x.source, x.source_id });
                    table.ForeignKey(
                        name: "FK_listing_labels_listings_source_source_id",
                        columns: x => new { x.source, x.source_id },
                        principalTable: "listings",
                        principalColumns: new[] { "source", "source_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listing_payloads",
                columns: table => new
                {
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    payload_hash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "listing_scores",
                columns: table => new
                {
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    profile = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    score = table.Column<int>(type: "int", nullable: false),
                    explanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    rules_hash = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    scored_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    search_key = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "listing_terms",
                columns: table => new
                {
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    term = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    found_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "market_counts",
                columns: table => new
                {
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    city = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    term = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    total = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_counts", x => new { x.source, x.snapshot_date, x.category, x.city, x.term });
                    table.ForeignKey(
                        name: "FK_market_counts_market_snapshots_source_snapshot_date",
                        columns: x => new { x.source, x.snapshot_date },
                        principalTable: "market_snapshots",
                        principalColumns: new[] { "source", "snapshot_date" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    from_status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    to_status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_application_events_applications_source_source_id",
                        columns: x => new { x.source, x.source_id },
                        principalTable: "applications",
                        principalColumns: new[] { "source", "source_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_application_events_listing",
                table: "application_events",
                columns: new[] { "source", "source_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_status",
                table: "applications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_collection_runs_search",
                table: "collection_runs",
                columns: new[] { "source", "search_key", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_listing_scores_profile_score",
                table: "listing_scores",
                columns: new[] { "profile", "score" });

            migrationBuilder.CreateIndex(
                name: "ix_listing_search_hits_search_key",
                table: "listing_search_hits",
                column: "search_key");

            migrationBuilder.CreateIndex(
                name: "ix_listings_first_seen_at",
                table: "listings",
                column: "first_seen_at");

            migrationBuilder.CreateIndex(
                name: "ix_market_listings_seen",
                table: "market_listings",
                columns: new[] { "source", "first_seen", "last_seen" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_events");

            migrationBuilder.DropTable(
                name: "candidate_experience");

            migrationBuilder.DropTable(
                name: "candidate_requirements");

            migrationBuilder.DropTable(
                name: "collection_runs");

            migrationBuilder.DropTable(
                name: "listing_descriptions");

            migrationBuilder.DropTable(
                name: "listing_labels");

            migrationBuilder.DropTable(
                name: "listing_payloads");

            migrationBuilder.DropTable(
                name: "listing_scores");

            migrationBuilder.DropTable(
                name: "listing_search_hits");

            migrationBuilder.DropTable(
                name: "listing_terms");

            migrationBuilder.DropTable(
                name: "market_counts");

            migrationBuilder.DropTable(
                name: "market_listings");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "market_snapshots");

            migrationBuilder.DropTable(
                name: "listings");
        }
    }
}
