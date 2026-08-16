using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixedDueDateReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "garage");

            migrationBuilder.AddColumn<DateOnly>(
                name: "FixedDueDate",
                schema: "garage",
                table: "Reminders",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "garage");

            migrationBuilder.DropColumn(
                name: "FixedDueDate",
                schema: "garage",
                table: "Reminders");
        }
    }
}
