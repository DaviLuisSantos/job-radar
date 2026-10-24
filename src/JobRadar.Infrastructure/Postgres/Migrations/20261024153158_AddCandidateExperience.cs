using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_experience",
                columns: table => new
                {
                    profile = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    years = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_experience", x => x.profile);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_experience");
        }
    }
}
