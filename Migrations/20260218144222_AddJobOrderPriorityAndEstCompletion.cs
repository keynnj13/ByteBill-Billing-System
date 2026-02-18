using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByteBill_BS.Migrations
{
    /// <inheritdoc />
    public partial class AddJobOrderPriorityAndEstCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedCompletionDate",
                table: "JOB_ORDERS",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "JOB_ORDERS",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedCompletionDate",
                table: "JOB_ORDERS");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "JOB_ORDERS");
        }
    }
}
