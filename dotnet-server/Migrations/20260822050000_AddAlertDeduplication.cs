using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TradingScanner._Data;

#nullable disable

namespace TradingScanner.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260822050000_AddAlertDeduplication")]
public partial class AddAlertDeduplication : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DeduplicationKey",
            schema: "scanner",
            table: "alert_history",
            type: "character varying(160)",
            maxLength: 160,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("""
            UPDATE scanner.alert_history
            SET "DeduplicationKey" = to_char("Timestamp" AT TIME ZONE 'UTC', 'YYYYMMDD')
                || ':' || upper("Symbol") || ':' || "AlertType" || ':' || "Setup" || ':' || "Id"
            """);

        migrationBuilder.CreateIndex(
            name: "IX_alert_history_DeduplicationKey",
            schema: "scanner",
            table: "alert_history",
            column: "DeduplicationKey",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_alert_history_DeduplicationKey",
            schema: "scanner",
            table: "alert_history");

        migrationBuilder.DropColumn(
            name: "DeduplicationKey",
            schema: "scanner",
            table: "alert_history");
    }
}
