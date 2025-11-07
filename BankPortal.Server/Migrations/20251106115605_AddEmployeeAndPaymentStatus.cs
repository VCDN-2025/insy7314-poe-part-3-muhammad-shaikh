using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankPortal.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeAndPaymentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEmployee",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "Payments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "Payments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SubmittedToSwift",
                table: "Payments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "Payments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerifiedByEmployeeId",
                table: "Payments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_VerifiedByEmployeeId",
                table: "Payments",
                column: "VerifiedByEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_VerifiedByEmployeeId",
                table: "Payments",
                column: "VerifiedByEmployeeId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_VerifiedByEmployeeId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_VerifiedByEmployeeId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IsEmployee",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "SubmittedToSwift",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VerifiedByEmployeeId",
                table: "Payments");
        }
    }
}
