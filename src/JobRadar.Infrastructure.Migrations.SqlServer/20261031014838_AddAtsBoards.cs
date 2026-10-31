using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations.SqlServer
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
                    platform = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    etag = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    last_status = table.Column<int>(type: "int", nullable: true),
                    last_swept_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    last_hit_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    consecutive_failures = table.Column<int>(type: "int", nullable: false),
                    jobs_total = table.Column<int>(type: "int", nullable: false),
                    jobs_kept = table.Column<int>(type: "int", nullable: false)
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
