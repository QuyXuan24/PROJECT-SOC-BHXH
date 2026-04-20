using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHXH_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewNoteToBhxhRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "BhxhRecords",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "BhxhRecords");
        }
    }
}
