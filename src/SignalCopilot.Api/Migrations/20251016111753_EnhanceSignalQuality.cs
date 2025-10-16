using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalCopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceSignalQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConsensusFactor",
                table: "Signals",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "EventCategory",
                table: "Signals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceCount",
                table: "Signals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "StanceAgreement",
                table: "Signals",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ClusterId",
                table: "Articles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EventCategory",
                table: "Articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RelatedTickers",
                table: "Articles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceTier",
                table: "Articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "Articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsensusFactor",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "EventCategory",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "SourceCount",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "StanceAgreement",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "ClusterId",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "EventCategory",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "RelatedTickers",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "SourceTier",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Articles");
        }
    }
}
