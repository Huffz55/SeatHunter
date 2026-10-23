using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeatHunter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexesAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HuntRequests_Status_NextScanTime",
                table: "HuntRequests",
                columns: new[] { "Status", "NextScanTime" });

            migrationBuilder.CreateIndex(
                name: "IX_HuntRequests_UserId",
                table: "HuntRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_HuntRequests_Status_NextScanTime",
                table: "HuntRequests");

            migrationBuilder.DropIndex(
                name: "IX_HuntRequests_UserId",
                table: "HuntRequests");
        }
    }
}
