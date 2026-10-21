using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "market_listings",
                columns: table => new
                {
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    city = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    company = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    work_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    salary_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    salary_period = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    salary_annual_min = table.Column<int>(type: "integer", nullable: true),
                    salary_annual_max = table.Column<int>(type: "integer", nullable: true),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    expected = table.Column<int>(type: "integer", nullable: false),
                    saved = table.Column<int>(type: "integer", nullable: false),
                    listings = table.Column<int>(type: "integer", nullable: false),
                    issues = table.Column<string>(type: "text", nullable: true),
                    taken_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_snapshots", x => new { x.source, x.snapshot_date });
                });

            migrationBuilder.CreateTable(
                name: "market_counts",
                columns: table => new
                {
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    city = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    term = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "ix_market_listings_seen",
                table: "market_listings",
                columns: new[] { "source", "first_seen", "last_seen" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "market_counts");

            migrationBuilder.DropTable(
                name: "market_listings");

            migrationBuilder.DropTable(
                name: "market_snapshots");
        }
    }
}
