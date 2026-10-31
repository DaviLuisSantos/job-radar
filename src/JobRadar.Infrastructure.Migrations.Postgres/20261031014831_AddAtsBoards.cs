using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddAtsBoards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ats_boards",
                columns: table => new
                {
                    platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    etag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_status = table.Column<int>(type: "integer", nullable: true),
                    last_swept_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_hit_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    jobs_total = table.Column<int>(type: "integer", nullable: false),
                    jobs_kept = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ats_boards", x => new { x.platform, x.slug });
                });

            migrationBuilder.CreateIndex(
                name: "ix_ats_boards_sweep",
                table: "ats_boards",
                columns: new[] { "platform", "last_swept_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ats_boards");
        }
    }
}
