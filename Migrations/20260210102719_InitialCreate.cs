using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByteBill_BS.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ROLES",
                columns: table => new
                {
                    RoleID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROLES", x => x.RoleID);
                });

            migrationBuilder.CreateTable(
                name: "SHOP",
                columns: table => new
                {
                    ShopID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ShopName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SHOP", x => x.ShopID);
                });

            migrationBuilder.CreateTable(
                name: "CUSTOMERS",
                columns: table => new
                {
                    CustomerID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopID = table.Column<long>(type: "bigint", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUSTOMERS", x => x.CustomerID);
                    table.ForeignKey(
                        name: "FK_CUSTOMERS_SHOP_ShopID",
                        column: x => x.ShopID,
                        principalTable: "SHOP",
                        principalColumn: "ShopID");
                });

            migrationBuilder.CreateTable(
                name: "INVENTORY_ITEMS",
                columns: table => new
                {
                    ItemID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopID = table.Column<long>(type: "bigint", nullable: false),
                    SKU = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    QtyOnHand = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ReorderLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INVENTORY_ITEMS", x => x.ItemID);
                    table.ForeignKey(
                        name: "FK_INVENTORY_ITEMS_SHOP_ShopID",
                        column: x => x.ShopID,
                        principalTable: "SHOP",
                        principalColumn: "ShopID");
                });

            migrationBuilder.CreateTable(
                name: "SERVICE_CATEGORY",
                columns: table => new
                {
                    ServiceCategoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopID = table.Column<long>(type: "bigint", nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SERVICE_CATEGORY", x => x.ServiceCategoryID);
                    table.ForeignKey(
                        name: "FK_SERVICE_CATEGORY_SHOP_ShopID",
                        column: x => x.ShopID,
                        principalTable: "SHOP",
                        principalColumn: "ShopID");
                });

            migrationBuilder.CreateTable(
                name: "USERS",
                columns: table => new
                {
                    UserID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopID = table.Column<long>(type: "bigint", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USERS", x => x.UserID);
                    table.ForeignKey(
                        name: "FK_USERS_SHOP_ShopID",
                        column: x => x.ShopID,
                        principalTable: "SHOP",
                        principalColumn: "ShopID");
                });

            migrationBuilder.CreateTable(
                name: "DEVICES",
                columns: table => new
                {
                    DeviceID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    DeviceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SerialNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DEVICES", x => x.DeviceID);
                    table.ForeignKey(
                        name: "FK_DEVICES_CUSTOMERS_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "CUSTOMERS",
                        principalColumn: "CustomerID");
                });

            migrationBuilder.CreateTable(
                name: "INVENTORY_TXN",
                columns: table => new
                {
                    InventoryTxnID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemID = table.Column<long>(type: "bigint", nullable: false),
                    TxnType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ReferenceID = table.Column<long>(type: "bigint", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INVENTORY_TXN", x => x.InventoryTxnID);
                    table.ForeignKey(
                        name: "FK_INVENTORY_TXN_INVENTORY_ITEMS_ItemID",
                        column: x => x.ItemID,
                        principalTable: "INVENTORY_ITEMS",
                        principalColumn: "ItemID");
                });

            migrationBuilder.CreateTable(
                name: "SERVICE_CATALOG",
                columns: table => new
                {
                    ServiceID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopID = table.Column<long>(type: "bigint", nullable: false),
                    ServiceCategoryID = table.Column<long>(type: "bigint", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SERVICE_CATALOG", x => x.ServiceID);
                    table.ForeignKey(
                        name: "FK_SERVICE_CATALOG_SERVICE_CATEGORY_ServiceCategoryID",
                        column: x => x.ServiceCategoryID,
                        principalTable: "SERVICE_CATEGORY",
                        principalColumn: "ServiceCategoryID");
                    table.ForeignKey(
                        name: "FK_SERVICE_CATALOG_SHOP_ShopID",
                        column: x => x.ShopID,
                        principalTable: "SHOP",
                        principalColumn: "ShopID");
                });

            migrationBuilder.CreateTable(
                name: "AUDIT_LOG",
                columns: table => new
                {
                    AuditLogID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopID = table.Column<long>(type: "bigint", nullable: false),
                    UserID = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityID = table.Column<long>(type: "bigint", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUDIT_LOG", x => x.AuditLogID);
                    table.ForeignKey(
                        name: "FK_AUDIT_LOG_SHOP_ShopID",
                        column: x => x.ShopID,
                        principalTable: "SHOP",
                        principalColumn: "ShopID");
                    table.ForeignKey(
                        name: "FK_AUDIT_LOG_USERS_UserID",
                        column: x => x.UserID,
                        principalTable: "USERS",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "PAYMENTS",
                columns: table => new
                {
                    PaymentID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopID = table.Column<long>(type: "bigint", nullable: false),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ReceivedByUserID = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Confirmed")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYMENTS", x => x.PaymentID);
                    table.ForeignKey(
                        name: "FK_PAYMENTS_CUSTOMERS_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "CUSTOMERS",
                        principalColumn: "CustomerID");
                    table.ForeignKey(
                        name: "FK_PAYMENTS_SHOP_ShopID",
                        column: x => x.ShopID,
                        principalTable: "SHOP",
                        principalColumn: "ShopID");
                    table.ForeignKey(
                        name: "FK_PAYMENTS_USERS_ReceivedByUserID",
                        column: x => x.ReceivedByUserID,
                        principalTable: "USERS",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "USER_ROLES",
                columns: table => new
                {
                    UserRoleID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<long>(type: "bigint", nullable: false),
                    RoleID = table.Column<long>(type: "bigint", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USER_ROLES", x => x.UserRoleID);
                    table.ForeignKey(
                        name: "FK_USER_ROLES_ROLES_RoleID",
                        column: x => x.RoleID,
                        principalTable: "ROLES",
                        principalColumn: "RoleID");
                    table.ForeignKey(
                        name: "FK_USER_ROLES_USERS_UserID",
                        column: x => x.UserID,
                        principalTable: "USERS",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "JOB_ORDERS",
                columns: table => new
                {
                    JobOrderID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopID = table.Column<long>(type: "bigint", nullable: false),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    DeviceID = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserID = table.Column<long>(type: "bigint", nullable: false),
                    AssignedTechUserID = table.Column<long>(type: "bigint", nullable: true),
                    JobOrderNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProblemReported = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DiagnosisNotes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Created"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JOB_ORDERS", x => x.JobOrderID);
                    table.ForeignKey(
                        name: "FK_JOB_ORDERS_CUSTOMERS_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "CUSTOMERS",
                        principalColumn: "CustomerID");
                    table.ForeignKey(
                        name: "FK_JOB_ORDERS_DEVICES_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "DEVICES",
                        principalColumn: "DeviceID");
                    table.ForeignKey(
                        name: "FK_JOB_ORDERS_SHOP_ShopID",
                        column: x => x.ShopID,
                        principalTable: "SHOP",
                        principalColumn: "ShopID");
                    table.ForeignKey(
                        name: "FK_JOB_ORDERS_USERS_AssignedTechUserID",
                        column: x => x.AssignedTechUserID,
                        principalTable: "USERS",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_JOB_ORDERS_USERS_CreatedByUserID",
                        column: x => x.CreatedByUserID,
                        principalTable: "USERS",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "PAYMONGO_TXN",
                columns: table => new
                {
                    PayMongoTxnID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentID = table.Column<long>(type: "bigint", nullable: false),
                    PayMongoPaymentIntentID = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PayMongoStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RawResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYMONGO_TXN", x => x.PayMongoTxnID);
                    table.ForeignKey(
                        name: "FK_PAYMONGO_TXN_PAYMENTS_PaymentID",
                        column: x => x.PaymentID,
                        principalTable: "PAYMENTS",
                        principalColumn: "PaymentID");
                });

            migrationBuilder.CreateTable(
                name: "INVOICES",
                columns: table => new
                {
                    InvoiceID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopID = table.Column<long>(type: "bigint", nullable: false),
                    JobOrderID = table.Column<long>(type: "bigint", nullable: false),
                    CustomerID = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    TotalAdjustments = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Unpaid"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INVOICES", x => x.InvoiceID);
                    table.ForeignKey(
                        name: "FK_INVOICES_CUSTOMERS_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "CUSTOMERS",
                        principalColumn: "CustomerID");
                    table.ForeignKey(
                        name: "FK_INVOICES_JOB_ORDERS_JobOrderID",
                        column: x => x.JobOrderID,
                        principalTable: "JOB_ORDERS",
                        principalColumn: "JobOrderID");
                    table.ForeignKey(
                        name: "FK_INVOICES_SHOP_ShopID",
                        column: x => x.ShopID,
                        principalTable: "SHOP",
                        principalColumn: "ShopID");
                });

            migrationBuilder.CreateTable(
                name: "JOB_ORDER_PARTS",
                columns: table => new
                {
                    JobOrderPartID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobOrderID = table.Column<long>(type: "bigint", nullable: false),
                    ItemID = table.Column<long>(type: "bigint", nullable: false),
                    QtyUsed = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, computedColumnSql: "[QtyUsed] * [UnitPrice]", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JOB_ORDER_PARTS", x => x.JobOrderPartID);
                    table.ForeignKey(
                        name: "FK_JOB_ORDER_PARTS_INVENTORY_ITEMS_ItemID",
                        column: x => x.ItemID,
                        principalTable: "INVENTORY_ITEMS",
                        principalColumn: "ItemID");
                    table.ForeignKey(
                        name: "FK_JOB_ORDER_PARTS_JOB_ORDERS_JobOrderID",
                        column: x => x.JobOrderID,
                        principalTable: "JOB_ORDERS",
                        principalColumn: "JobOrderID");
                });

            migrationBuilder.CreateTable(
                name: "JOB_ORDER_SERVICES",
                columns: table => new
                {
                    JobOrderServiceID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobOrderID = table.Column<long>(type: "bigint", nullable: false),
                    ServiceID = table.Column<long>(type: "bigint", nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, computedColumnSql: "[Qty] * [UnitPrice]", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JOB_ORDER_SERVICES", x => x.JobOrderServiceID);
                    table.ForeignKey(
                        name: "FK_JOB_ORDER_SERVICES_JOB_ORDERS_JobOrderID",
                        column: x => x.JobOrderID,
                        principalTable: "JOB_ORDERS",
                        principalColumn: "JobOrderID");
                    table.ForeignKey(
                        name: "FK_JOB_ORDER_SERVICES_SERVICE_CATALOG_ServiceID",
                        column: x => x.ServiceID,
                        principalTable: "SERVICE_CATALOG",
                        principalColumn: "ServiceID");
                });

            migrationBuilder.CreateTable(
                name: "JOB_ORDER_STATUS_HISTORY",
                columns: table => new
                {
                    JobOrderStatusHistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobOrderID = table.Column<long>(type: "bigint", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChangedByUserID = table.Column<long>(type: "bigint", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    Remarks = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JOB_ORDER_STATUS_HISTORY", x => x.JobOrderStatusHistoryID);
                    table.ForeignKey(
                        name: "FK_JOB_ORDER_STATUS_HISTORY_JOB_ORDERS_JobOrderID",
                        column: x => x.JobOrderID,
                        principalTable: "JOB_ORDERS",
                        principalColumn: "JobOrderID");
                    table.ForeignKey(
                        name: "FK_JOB_ORDER_STATUS_HISTORY_USERS_ChangedByUserID",
                        column: x => x.ChangedByUserID,
                        principalTable: "USERS",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "ACCOUNTING_ENTRY",
                columns: table => new
                {
                    AccountingEntryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopID = table.Column<long>(type: "bigint", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceInvoiceID = table.Column<long>(type: "bigint", nullable: true),
                    SourcePaymentID = table.Column<long>(type: "bigint", nullable: true),
                    EntryDate = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    AccountCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    Memo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCOUNTING_ENTRY", x => x.AccountingEntryID);
                    table.ForeignKey(
                        name: "FK_ACCOUNTING_ENTRY_INVOICES_SourceInvoiceID",
                        column: x => x.SourceInvoiceID,
                        principalTable: "INVOICES",
                        principalColumn: "InvoiceID");
                    table.ForeignKey(
                        name: "FK_ACCOUNTING_ENTRY_PAYMENTS_SourcePaymentID",
                        column: x => x.SourcePaymentID,
                        principalTable: "PAYMENTS",
                        principalColumn: "PaymentID");
                    table.ForeignKey(
                        name: "FK_ACCOUNTING_ENTRY_SHOP_ShopID",
                        column: x => x.ShopID,
                        principalTable: "SHOP",
                        principalColumn: "ShopID");
                });

            migrationBuilder.CreateTable(
                name: "CREDIT_DEBIT_ADJUSTMENT",
                columns: table => new
                {
                    AdjustmentID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceID = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserID = table.Column<long>(type: "bigint", nullable: false),
                    AdjustmentType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CREDIT_DEBIT_ADJUSTMENT", x => x.AdjustmentID);
                    table.ForeignKey(
                        name: "FK_CREDIT_DEBIT_ADJUSTMENT_INVOICES_InvoiceID",
                        column: x => x.InvoiceID,
                        principalTable: "INVOICES",
                        principalColumn: "InvoiceID");
                    table.ForeignKey(
                        name: "FK_CREDIT_DEBIT_ADJUSTMENT_USERS_CreatedByUserID",
                        column: x => x.CreatedByUserID,
                        principalTable: "USERS",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "INVOICE_LINES",
                columns: table => new
                {
                    InvoiceLineID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceID = table.Column<long>(type: "bigint", nullable: false),
                    LineType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, computedColumnSql: "[Qty] * [UnitPrice]", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INVOICE_LINES", x => x.InvoiceLineID);
                    table.ForeignKey(
                        name: "FK_INVOICE_LINES_INVOICES_InvoiceID",
                        column: x => x.InvoiceID,
                        principalTable: "INVOICES",
                        principalColumn: "InvoiceID");
                });

            migrationBuilder.CreateTable(
                name: "PAYMENT_ALLOCATION",
                columns: table => new
                {
                    PaymentAllocationID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentID = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceID = table.Column<long>(type: "bigint", nullable: false),
                    AmountApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYMENT_ALLOCATION", x => x.PaymentAllocationID);
                    table.ForeignKey(
                        name: "FK_PAYMENT_ALLOCATION_INVOICES_InvoiceID",
                        column: x => x.InvoiceID,
                        principalTable: "INVOICES",
                        principalColumn: "InvoiceID");
                    table.ForeignKey(
                        name: "FK_PAYMENT_ALLOCATION_PAYMENTS_PaymentID",
                        column: x => x.PaymentID,
                        principalTable: "PAYMENTS",
                        principalColumn: "PaymentID");
                });

            migrationBuilder.CreateTable(
                name: "XERO_SYNC_LOG",
                columns: table => new
                {
                    XeroSyncLogID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShopID = table.Column<long>(type: "bigint", nullable: false),
                    SyncedByUserID = table.Column<long>(type: "bigint", nullable: true),
                    SyncType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InvoiceID = table.Column<long>(type: "bigint", nullable: true),
                    PaymentID = table.Column<long>(type: "bigint", nullable: true),
                    AccountingEntryID = table.Column<long>(type: "bigint", nullable: true),
                    XeroRecordID = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    Message = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XERO_SYNC_LOG", x => x.XeroSyncLogID);
                    table.ForeignKey(
                        name: "FK_XERO_SYNC_LOG_ACCOUNTING_ENTRY_AccountingEntryID",
                        column: x => x.AccountingEntryID,
                        principalTable: "ACCOUNTING_ENTRY",
                        principalColumn: "AccountingEntryID");
                    table.ForeignKey(
                        name: "FK_XERO_SYNC_LOG_INVOICES_InvoiceID",
                        column: x => x.InvoiceID,
                        principalTable: "INVOICES",
                        principalColumn: "InvoiceID");
                    table.ForeignKey(
                        name: "FK_XERO_SYNC_LOG_PAYMENTS_PaymentID",
                        column: x => x.PaymentID,
                        principalTable: "PAYMENTS",
                        principalColumn: "PaymentID");
                    table.ForeignKey(
                        name: "FK_XERO_SYNC_LOG_SHOP_ShopID",
                        column: x => x.ShopID,
                        principalTable: "SHOP",
                        principalColumn: "ShopID");
                    table.ForeignKey(
                        name: "FK_XERO_SYNC_LOG_USERS_SyncedByUserID",
                        column: x => x.SyncedByUserID,
                        principalTable: "USERS",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACCOUNTING_ENTRY_ShopID",
                table: "ACCOUNTING_ENTRY",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_ACCOUNTING_ENTRY_SourceInvoiceID",
                table: "ACCOUNTING_ENTRY",
                column: "SourceInvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_ACCOUNTING_ENTRY_SourcePaymentID",
                table: "ACCOUNTING_ENTRY",
                column: "SourcePaymentID");

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOG_CreatedAt",
                table: "AUDIT_LOG",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOG_ShopID",
                table: "AUDIT_LOG",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOG_UserID",
                table: "AUDIT_LOG",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_CREDIT_DEBIT_ADJUSTMENT_CreatedByUserID",
                table: "CREDIT_DEBIT_ADJUSTMENT",
                column: "CreatedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_CREDIT_DEBIT_ADJUSTMENT_InvoiceID",
                table: "CREDIT_DEBIT_ADJUSTMENT",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMERS_ShopID",
                table: "CUSTOMERS",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_DEVICES_CustomerID",
                table: "DEVICES",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_INVENTORY_ITEMS_ShopID",
                table: "INVENTORY_ITEMS",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_INVENTORY_ITEMS_ShopID_SKU",
                table: "INVENTORY_ITEMS",
                columns: new[] { "ShopID", "SKU" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INVENTORY_TXN_ItemID",
                table: "INVENTORY_TXN",
                column: "ItemID");

            migrationBuilder.CreateIndex(
                name: "IX_INVOICE_LINES_InvoiceID",
                table: "INVOICE_LINES",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_INVOICES_CustomerID",
                table: "INVOICES",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_INVOICES_JobOrderID",
                table: "INVOICES",
                column: "JobOrderID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INVOICES_ShopID",
                table: "INVOICES",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_INVOICES_ShopID_InvoiceNo",
                table: "INVOICES",
                columns: new[] { "ShopID", "InvoiceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDER_PARTS_ItemID",
                table: "JOB_ORDER_PARTS",
                column: "ItemID");

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDER_PARTS_JobOrderID",
                table: "JOB_ORDER_PARTS",
                column: "JobOrderID");

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDER_SERVICES_JobOrderID",
                table: "JOB_ORDER_SERVICES",
                column: "JobOrderID");

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDER_SERVICES_ServiceID",
                table: "JOB_ORDER_SERVICES",
                column: "ServiceID");

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDER_STATUS_HISTORY_ChangedByUserID",
                table: "JOB_ORDER_STATUS_HISTORY",
                column: "ChangedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDER_STATUS_HISTORY_JobOrderID",
                table: "JOB_ORDER_STATUS_HISTORY",
                column: "JobOrderID");

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDERS_AssignedTechUserID",
                table: "JOB_ORDERS",
                column: "AssignedTechUserID");

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDERS_CreatedByUserID",
                table: "JOB_ORDERS",
                column: "CreatedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDERS_CustomerID",
                table: "JOB_ORDERS",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDERS_DeviceID",
                table: "JOB_ORDERS",
                column: "DeviceID");

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDERS_ShopID",
                table: "JOB_ORDERS",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_JOB_ORDERS_ShopID_JobOrderNo",
                table: "JOB_ORDERS",
                columns: new[] { "ShopID", "JobOrderNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_ALLOCATION_InvoiceID",
                table: "PAYMENT_ALLOCATION",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_ALLOCATION_PaymentID",
                table: "PAYMENT_ALLOCATION",
                column: "PaymentID");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_ALLOCATION_PaymentID_InvoiceID",
                table: "PAYMENT_ALLOCATION",
                columns: new[] { "PaymentID", "InvoiceID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENTS_CustomerID",
                table: "PAYMENTS",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENTS_ReceivedByUserID",
                table: "PAYMENTS",
                column: "ReceivedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENTS_ShopID",
                table: "PAYMENTS",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMONGO_TXN_PaymentID",
                table: "PAYMONGO_TXN",
                column: "PaymentID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ROLES_RoleName",
                table: "ROLES",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SERVICE_CATALOG_ServiceCategoryID",
                table: "SERVICE_CATALOG",
                column: "ServiceCategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_SERVICE_CATALOG_ShopID",
                table: "SERVICE_CATALOG",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_SERVICE_CATALOG_ShopID_ServiceName",
                table: "SERVICE_CATALOG",
                columns: new[] { "ShopID", "ServiceName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SERVICE_CATEGORY_ShopID",
                table: "SERVICE_CATEGORY",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_SERVICE_CATEGORY_ShopID_CategoryName",
                table: "SERVICE_CATEGORY",
                columns: new[] { "ShopID", "CategoryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SHOP_ShopCode",
                table: "SHOP",
                column: "ShopCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USER_ROLES_RoleID",
                table: "USER_ROLES",
                column: "RoleID");

            migrationBuilder.CreateIndex(
                name: "IX_USER_ROLES_UserID",
                table: "USER_ROLES",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_USER_ROLES_UserID_RoleID",
                table: "USER_ROLES",
                columns: new[] { "UserID", "RoleID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USERS_ShopID",
                table: "USERS",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_USERS_ShopID_UserName",
                table: "USERS",
                columns: new[] { "ShopID", "UserName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XERO_SYNC_LOG_AccountingEntryID",
                table: "XERO_SYNC_LOG",
                column: "AccountingEntryID");

            migrationBuilder.CreateIndex(
                name: "IX_XERO_SYNC_LOG_InvoiceID",
                table: "XERO_SYNC_LOG",
                column: "InvoiceID");

            migrationBuilder.CreateIndex(
                name: "IX_XERO_SYNC_LOG_PaymentID",
                table: "XERO_SYNC_LOG",
                column: "PaymentID");

            migrationBuilder.CreateIndex(
                name: "IX_XERO_SYNC_LOG_ShopID",
                table: "XERO_SYNC_LOG",
                column: "ShopID");

            migrationBuilder.CreateIndex(
                name: "IX_XERO_SYNC_LOG_SyncedByUserID",
                table: "XERO_SYNC_LOG",
                column: "SyncedByUserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AUDIT_LOG");

            migrationBuilder.DropTable(
                name: "CREDIT_DEBIT_ADJUSTMENT");

            migrationBuilder.DropTable(
                name: "INVENTORY_TXN");

            migrationBuilder.DropTable(
                name: "INVOICE_LINES");

            migrationBuilder.DropTable(
                name: "JOB_ORDER_PARTS");

            migrationBuilder.DropTable(
                name: "JOB_ORDER_SERVICES");

            migrationBuilder.DropTable(
                name: "JOB_ORDER_STATUS_HISTORY");

            migrationBuilder.DropTable(
                name: "PAYMENT_ALLOCATION");

            migrationBuilder.DropTable(
                name: "PAYMONGO_TXN");

            migrationBuilder.DropTable(
                name: "USER_ROLES");

            migrationBuilder.DropTable(
                name: "XERO_SYNC_LOG");

            migrationBuilder.DropTable(
                name: "INVENTORY_ITEMS");

            migrationBuilder.DropTable(
                name: "SERVICE_CATALOG");

            migrationBuilder.DropTable(
                name: "ROLES");

            migrationBuilder.DropTable(
                name: "ACCOUNTING_ENTRY");

            migrationBuilder.DropTable(
                name: "SERVICE_CATEGORY");

            migrationBuilder.DropTable(
                name: "INVOICES");

            migrationBuilder.DropTable(
                name: "PAYMENTS");

            migrationBuilder.DropTable(
                name: "JOB_ORDERS");

            migrationBuilder.DropTable(
                name: "DEVICES");

            migrationBuilder.DropTable(
                name: "USERS");

            migrationBuilder.DropTable(
                name: "CUSTOMERS");

            migrationBuilder.DropTable(
                name: "SHOP");
        }
    }
}
