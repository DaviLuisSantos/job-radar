using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.key);
                    table.CheckConstraint("ck_settings_value_json", "ISJSON([value]) = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "settings");
        }
    }
}
