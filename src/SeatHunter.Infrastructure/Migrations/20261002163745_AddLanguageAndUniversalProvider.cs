using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeatHunter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageAndUniversalProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "HuntRequests",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "HuntRequests");
        }
    }
}
