using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NetForum.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCategorySeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Description", "DisplayOrder", "Icon", "Name", "Slug" },
                values: new object[,]
                {
                    { 1, "General chatter, discussions, and off-topic things.", 1, "bi-chat-left-dots", "General", "general" },
                    { 2, "Discuss code, web development, algorithms, and tech stacks.", 2, "bi-code-slash", "Programming", "programming" },
                    { 3, "Got a technical question? Ask the community for help.", 3, "bi-question-circle", "Q&A / Support", "qa" },
                    { 4, "Official updates, guidelines, and site news.", 4, "bi-megaphone", "Announcements", "announcements" }
                });
        }
    }
}
