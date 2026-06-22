using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddABC_CategoryToClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ABC_Category",
                table: "Clients",
                type: "TEXT",
                maxLength: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ABC_Category",
                table: "Clients");
        }
    }
}
