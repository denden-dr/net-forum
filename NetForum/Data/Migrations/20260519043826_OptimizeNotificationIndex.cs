using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetForum.Data.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeNotificationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientId_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId_CreatedAt_IsRead",
                table: "Notifications",
                columns: new[] { "RecipientId", "CreatedAt", "IsRead" },
                descending: new[] { false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientId_CreatedAt_IsRead",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "RecipientId", "IsRead", "CreatedAt" });
        }
    }
}
