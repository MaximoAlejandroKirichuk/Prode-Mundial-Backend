using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookDeliveryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveries_PaymentId",
                table: "WebhookDeliveries");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "WebhookDeliveries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_PaymentId_Status",
                table: "WebhookDeliveries",
                columns: new[] { "PaymentId", "Status" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveries_PaymentId_Status",
                table: "WebhookDeliveries");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "WebhookDeliveries");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_PaymentId",
                table: "WebhookDeliveries",
                column: "PaymentId",
                unique: true);
        }
    }
}
