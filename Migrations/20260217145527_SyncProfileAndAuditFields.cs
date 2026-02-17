using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByteBill_BS.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Columns already applied via Database/AddProfileAndAuditFields.sql.
    /// This migration exists only to sync the EF model snapshot.
    /// </summary>
    public partial class SyncProfileAndAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: columns added via raw SQL script
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op
        }
    }
}
