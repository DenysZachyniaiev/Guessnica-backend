using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guessnica_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRiddleTimeAndDistance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxDistanceMeters",
                table: "Riddles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitSeconds",
                table: "Riddles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxDistanceMeters",
                table: "Riddles");

            migrationBuilder.DropColumn(
                name: "TimeLimitSeconds",
                table: "Riddles");
        }
    }
}
