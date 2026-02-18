using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByteBill_BS.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "JOB_ORDERS",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldDefaultValue: "Created");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedDate",
                table: "JOB_ORDERS",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "JOB_ORDERS",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedDate",
                table: "INVOICES",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "INVOICES",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedDate",
                table: "JOB_ORDERS");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "JOB_ORDERS");

            migrationBuilder.DropColumn(
                name: "ArchivedDate",
                table: "INVOICES");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "INVOICES");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "JOB_ORDERS",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Created",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldDefaultValue: "Pending");
        }
    }
}
