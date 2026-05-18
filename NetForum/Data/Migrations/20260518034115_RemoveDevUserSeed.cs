using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetForum.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDevUserSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "Email", "EmailConfirmationRequestsCount", "EmailConfirmed", "LastEmailConfirmationRequestAt", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "Role", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), 0, "00000000-0000-0000-0000-000000000000", new DateTimeOffset(new DateTime(2026, 5, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "devuser@netforum.com", 0, true, null, false, null, "DEVUSER@NETFORUM.COM", "DEVUSER", null, null, false, "Member", "00000000-0000-0000-0000-000000000000", false, "DevUser" });
        }
    }
}
