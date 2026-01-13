using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guessnica_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmittedCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SubmittedLatitude",
                table: "UserRiddles",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubmittedLongitude",
                table: "UserRiddles",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmittedLatitude",
                table: "UserRiddles");

            migrationBuilder.DropColumn(
                name: "SubmittedLongitude",
                table: "UserRiddles");
        }
    }
}
