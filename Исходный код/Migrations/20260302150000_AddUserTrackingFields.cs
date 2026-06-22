using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Добавляем поля UpdatedByUserId в основные таблицы
            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Clients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Deals",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Expenses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Tasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: true);

            // Создаем индексы для оптимизации запросов
            migrationBuilder.CreateIndex(
                name: "IX_Clients_CreatedByUserId",
                table: "Clients",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_UpdatedByUserId",
                table: "Clients",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_CreatedByUserId",
                table: "Deals",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_AssignedToUserId",
                table: "Deals",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_UpdatedByUserId",
                table: "Deals",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreatedByUserId",
                table: "Expenses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_UpdatedByUserId",
                table: "Expenses",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_CreatedByUserId",
                table: "Tasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssignedToUserId",
                table: "Tasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_UpdatedByUserId",
                table: "Tasks",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_AssignedToUserId",
                table: "CalendarEvents",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_UpdatedByUserId",
                table: "CalendarEvents",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_UserId",
                table: "Interactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Удаляем индексы
            migrationBuilder.DropIndex(
                name: "IX_Clients_CreatedByUserId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_UpdatedByUserId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Deals_CreatedByUserId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_AssignedToUserId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_UpdatedByUserId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_CreatedByUserId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_UpdatedByUserId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_CreatedByUserId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_AssignedToUserId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_UpdatedByUserId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_AssignedToUserId",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_UpdatedByUserId",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_Interactions_UserId",
                table: "Interactions");

            // Удаляем поля UpdatedByUserId
            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Clients");
        }
    }
}
