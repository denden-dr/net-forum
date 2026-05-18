using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetForum.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmailConfirmationRequestsCount",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastEmailConfirmationRequestAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "EmailConfirmationRequestsCount", "LastEmailConfirmationRequestAt" },
                values: new object[] { 0, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailConfirmationRequestsCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastEmailConfirmationRequestAt",
                table: "Users");
        }
    }
}
