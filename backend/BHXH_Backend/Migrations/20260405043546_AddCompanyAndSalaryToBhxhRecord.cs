using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHXH_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyAndSalaryToBhxhRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "BhxhRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Salary",
                table: "BhxhRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "BhxhRecords");

            migrationBuilder.DropColumn(
                name: "Salary",
                table: "BhxhRecords");
        }
    }
}
