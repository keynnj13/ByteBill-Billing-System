-- ═══════════════════════════════════════════════════════════════════════
-- Migration: BIR Tax Compliance (Philippine VAT / Non-VAT)
-- Date: 2025-06-XX
-- Description:
--   1. Add TIN, IsVatRegistered, TaxRate to SHOP table
--   2. Add DiscountAmount, VatableSales, VatExemptSales, ZeroRatedSales, VatAmount to INVOICES
--   3. Create INVOICE_DISCOUNT table for SC/PWD/Promo discounts
-- ═══════════════════════════════════════════════════════════════════════

-- ─── 1. SHOP: Tax registration columns ──────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SHOP') AND name = 'TIN')
BEGIN
    ALTER TABLE [SHOP] ADD [TIN] NVARCHAR(20) NULL;
    PRINT 'Added SHOP.TIN';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SHOP') AND name = 'IsVatRegistered')
BEGIN
    ALTER TABLE [SHOP] ADD [IsVatRegistered] BIT NOT NULL CONSTRAINT DF_SHOP_IsVatRegistered DEFAULT 1;
    PRINT 'Added SHOP.IsVatRegistered (default: 1 = VAT-registered)';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SHOP') AND name = 'TaxRate')
BEGIN
    ALTER TABLE [SHOP] ADD [TaxRate] DECIMAL(18,2) NOT NULL CONSTRAINT DF_SHOP_TaxRate DEFAULT 12.00;
    PRINT 'Added SHOP.TaxRate (default: 12%)';
END
GO

-- ─── 2. INVOICES: BIR tax breakdown columns ─────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('INVOICES') AND name = 'DiscountAmount')
BEGIN
    ALTER TABLE [INVOICES] ADD [DiscountAmount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_INVOICES_DiscountAmount DEFAULT 0;
    PRINT 'Added INVOICES.DiscountAmount';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('INVOICES') AND name = 'VatableSales')
BEGIN
    ALTER TABLE [INVOICES] ADD [VatableSales] DECIMAL(18,2) NOT NULL CONSTRAINT DF_INVOICES_VatableSales DEFAULT 0;
    PRINT 'Added INVOICES.VatableSales';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('INVOICES') AND name = 'VatExemptSales')
BEGIN
    ALTER TABLE [INVOICES] ADD [VatExemptSales] DECIMAL(18,2) NOT NULL CONSTRAINT DF_INVOICES_VatExemptSales DEFAULT 0;
    PRINT 'Added INVOICES.VatExemptSales';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('INVOICES') AND name = 'ZeroRatedSales')
BEGIN
    ALTER TABLE [INVOICES] ADD [ZeroRatedSales] DECIMAL(18,2) NOT NULL CONSTRAINT DF_INVOICES_ZeroRatedSales DEFAULT 0;
    PRINT 'Added INVOICES.ZeroRatedSales';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('INVOICES') AND name = 'VatAmount')
BEGIN
    ALTER TABLE [INVOICES] ADD [VatAmount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_INVOICES_VatAmount DEFAULT 0;
    PRINT 'Added INVOICES.VatAmount';
END
GO

-- ─── 3. INVOICE_DISCOUNT table ──────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('INVOICE_DISCOUNT') AND type = 'U')
BEGIN
    CREATE TABLE [INVOICE_DISCOUNT] (
        [InvoiceDiscountID] BIGINT IDENTITY(1,1) NOT NULL,
        [InvoiceID]         BIGINT NOT NULL,
        [DiscountType]      NVARCHAR(20) NOT NULL,          -- SeniorCitizen, PWD, Promo
        [Label]             NVARCHAR(120) NOT NULL,          -- Display label on receipt
        [Percentage]        DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Amount]            DECIMAL(18,2) NOT NULL DEFAULT 0,
        [IsVatExempt]       BIT NOT NULL DEFAULT 0,
        [BeneficiaryIdNo]   NVARCHAR(30) NULL,               -- SC/PWD ID for BIR
        [BeneficiaryName]   NVARCHAR(120) NULL,              -- SC/PWD name
        [AppliedByUserID]   BIGINT NOT NULL,
        [AppliedAt]         DATETIME2(0) NOT NULL DEFAULT SYSDATETIME(),

        CONSTRAINT [PK_INVOICE_DISCOUNT] PRIMARY KEY CLUSTERED ([InvoiceDiscountID]),
        CONSTRAINT [FK_INVOICE_DISCOUNT_Invoice]  FOREIGN KEY ([InvoiceID])       REFERENCES [INVOICES]([InvoiceID]),
        CONSTRAINT [FK_INVOICE_DISCOUNT_User]     FOREIGN KEY ([AppliedByUserID]) REFERENCES [USERS]([UserID])
    );

    CREATE INDEX [IX_INVOICE_DISCOUNT_InvoiceID]       ON [INVOICE_DISCOUNT] ([InvoiceID]);
    CREATE INDEX [IX_INVOICE_DISCOUNT_AppliedByUserID] ON [INVOICE_DISCOUNT] ([AppliedByUserID]);

    PRINT 'Created INVOICE_DISCOUNT table';
END
GO

PRINT '✅ BIR Tax Compliance migration complete.';
GO
