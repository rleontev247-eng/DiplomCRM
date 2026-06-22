using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingColumnsToDealsAndExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Tasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "Expenses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Expenses",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Expenses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Deals",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Deals",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Clients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ToEmail = table.Column<string>(type: "TEXT", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    FromEmail = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    MessageId = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpHost = table.Column<string>(type: "TEXT", nullable: false),
                    SmtpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    UseSsl = table.Column<bool>(type: "INTEGER", nullable: false),
                    DealId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    NotificationId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AttachmentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageSize = table.Column<long>(type: "INTEGER", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailMessages_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmailMessages_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmailMessages_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SmsMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    SenderName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    DealId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    NotificationId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Cost = table.Column<decimal>(type: "TEXT", nullable: false),
                    CharacterCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SmsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsMessages_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SmsMessages_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SmsMessages_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssignedToUserId",
                table: "Tasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_CreatedByUserId",
                table: "Tasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_UpdatedByUserId",
                table: "Tasks",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_UserId",
                table: "Interactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreatedByUserId",
                table: "Expenses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_UpdatedByUserId",
                table: "Expenses",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_AssignedToUserId",
                table: "Deals",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_CreatedByUserId",
                table: "Deals",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_UpdatedByUserId",
                table: "Deals",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CreatedByUserId",
                table: "Clients",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_UpdatedByUserId",
                table: "Clients",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_AssignedToUserId",
                table: "CalendarEvents",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_CreatedByUserId",
                table: "CalendarEvents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_UpdatedByUserId",
                table: "CalendarEvents",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_ClientId",
                table: "EmailMessages",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_DealId",
                table: "EmailMessages",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_NotificationId",
                table: "EmailMessages",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_ClientId",
                table: "SmsMessages",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_DealId",
                table: "SmsMessages",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_NotificationId",
                table: "SmsMessages",
                column: "NotificationId");

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_Users_AssignedToUserId",
                table: "CalendarEvents",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_Users_CreatedByUserId",
                table: "CalendarEvents",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_Users_UpdatedByUserId",
                table: "CalendarEvents",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Users_CreatedByUserId",
                table: "Clients",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Users_UpdatedByUserId",
                table: "Clients",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Users_AssignedToUserId",
                table: "Deals",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Users_CreatedByUserId",
                table: "Deals",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Users_UpdatedByUserId",
                table: "Deals",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Users_CreatedByUserId",
                table: "Expenses",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Users_UpdatedByUserId",
                table: "Expenses",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Interactions_Users_UserId",
                table: "Interactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Users_AssignedToUserId",
                table: "Tasks",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Users_CreatedByUserId",
                table: "Tasks",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Users_UpdatedByUserId",
                table: "Tasks",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_Users_AssignedToUserId",
                table: "CalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_Users_CreatedByUserId",
                table: "CalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_Users_UpdatedByUserId",
                table: "CalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Users_CreatedByUserId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Users_UpdatedByUserId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Users_AssignedToUserId",
                table: "Deals");

            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Users_CreatedByUserId",
                table: "Deals");

            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Users_UpdatedByUserId",
                table: "Deals");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Users_CreatedByUserId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Users_UpdatedByUserId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Interactions_Users_UserId",
                table: "Interactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Users_AssignedToUserId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Users_CreatedByUserId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Users_UpdatedByUserId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropTable(
                name: "SmsMessages");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_AssignedToUserId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_CreatedByUserId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_UpdatedByUserId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Interactions_UserId",
                table: "Interactions");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_CreatedByUserId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_UpdatedByUserId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Deals_AssignedToUserId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_CreatedByUserId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_UpdatedByUserId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Clients_CreatedByUserId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_UpdatedByUserId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_AssignedToUserId",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_CreatedByUserId",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_UpdatedByUserId",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "CalendarEvents");
        }
    }
}
