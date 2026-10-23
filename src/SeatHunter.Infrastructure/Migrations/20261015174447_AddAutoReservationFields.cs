using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeatHunter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoReservationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoReserveEnabled",
                table: "HuntRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReservationCartUrl",
                table: "HuntRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReservedUntil",
                table: "HuntRequests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoReserveEnabled",
                table: "HuntRequests");

            migrationBuilder.DropColumn(
                name: "ReservationCartUrl",
                table: "HuntRequests");

            migrationBuilder.DropColumn(
                name: "ReservedUntil",
                table: "HuntRequests");
        }
    }
}
