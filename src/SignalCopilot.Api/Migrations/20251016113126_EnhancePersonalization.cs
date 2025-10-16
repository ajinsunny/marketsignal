using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalCopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnhancePersonalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcquiredAt",
                table: "Holdings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Intent",
                table: "Holdings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CashBuffer",
                table: "AspNetUsers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RiskProfile",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcquiredAt",
                table: "Holdings");

            migrationBuilder.DropColumn(
                name: "Intent",
                table: "Holdings");

            migrationBuilder.DropColumn(
                name: "CashBuffer",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RiskProfile",
                table: "AspNetUsers");
        }
    }
}
