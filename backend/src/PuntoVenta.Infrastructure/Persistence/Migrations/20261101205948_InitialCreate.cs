using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PuntoVenta.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "categories",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_categories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "customers",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                DocNumber = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                Address = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_customers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "document_series",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DocumentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                LastNumber = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_document_series", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "products",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Sku = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                CategoryId = table.Column<int>(type: "integer", nullable: false),
                Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Stock = table.Column<int>(type: "integer", nullable: false),
                MinStock = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_products", x => x.Id);
                table.CheckConstraint("ck_products_price_positive", "\"Price\" > 0");
                table.CheckConstraint("ck_products_stock_non_negative", "\"Stock\" >= 0");
                table.ForeignKey(
                    name: "FK_products_categories_CategoryId",
                    column: x => x.CategoryId,
                    principalTable: "categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "cash_sessions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CashierId = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                OpeningAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                ExpectedCash = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                CountedCash = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                Difference = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cash_sessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_cash_sessions_users_CashierId",
                    column: x => x.CashierId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "sales",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DocumentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Series = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                Number = table.Column<int>(type: "integer", nullable: false),
                CustomerId = table.Column<int>(type: "integer", nullable: true),
                CashierId = table.Column<int>(type: "integer", nullable: false),
                CashSessionId = table.Column<int>(type: "integer", nullable: false),
                GrossAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Discount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Igv = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                AmountPaid = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Change = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                VoidedById = table.Column<int>(type: "integer", nullable: true),
                VoidReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sales", x => x.Id);
                table.ForeignKey(
                    name: "FK_sales_cash_sessions_CashSessionId",
                    column: x => x.CashSessionId,
                    principalTable: "cash_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_sales_customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_sales_users_CashierId",
                    column: x => x.CashierId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_sales_users_VoidedById",
                    column: x => x.VoidedById,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "payments",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SaleId = table.Column<int>(type: "integer", nullable: false),
                Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payments", x => x.Id);
                table.ForeignKey(
                    name: "FK_payments_sales_SaleId",
                    column: x => x.SaleId,
                    principalTable: "sales",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "sale_items",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SaleId = table.Column<int>(type: "integer", nullable: false),
                ProductId = table.Column<int>(type: "integer", nullable: false),
                ProductName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                LineTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sale_items", x => x.Id);
                table.ForeignKey(
                    name: "FK_sale_items_products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_sale_items_sales_SaleId",
                    column: x => x.SaleId,
                    principalTable: "sales",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_cash_sessions_CashierId_Status",
            table: "cash_sessions",
            columns: new[] { "CashierId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_categories_Name",
            table: "categories",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_customers_DocNumber",
            table: "customers",
            column: "DocNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_document_series_Code",
            table: "document_series",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_payments_SaleId",
            table: "payments",
            column: "SaleId");

        migrationBuilder.CreateIndex(
            name: "IX_products_CategoryId",
            table: "products",
            column: "CategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_products_Name",
            table: "products",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_products_Sku",
            table: "products",
            column: "Sku",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_sale_items_ProductId",
            table: "sale_items",
            column: "ProductId");

        migrationBuilder.CreateIndex(
            name: "IX_sale_items_SaleId",
            table: "sale_items",
            column: "SaleId");

        migrationBuilder.CreateIndex(
            name: "IX_sales_CashierId",
            table: "sales",
            column: "CashierId");

        migrationBuilder.CreateIndex(
            name: "IX_sales_CashSessionId",
            table: "sales",
            column: "CashSessionId");

        migrationBuilder.CreateIndex(
            name: "IX_sales_CreatedAt",
            table: "sales",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_sales_CustomerId",
            table: "sales",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_sales_Series_Number",
            table: "sales",
            columns: new[] { "Series", "Number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_sales_VoidedById",
            table: "sales",
            column: "VoidedById");

        migrationBuilder.CreateIndex(
            name: "IX_users_Email",
            table: "users",
            column: "Email",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "document_series");

        migrationBuilder.DropTable(
            name: "payments");

        migrationBuilder.DropTable(
            name: "sale_items");

        migrationBuilder.DropTable(
            name: "products");

        migrationBuilder.DropTable(
            name: "sales");

        migrationBuilder.DropTable(
            name: "categories");

        migrationBuilder.DropTable(
            name: "cash_sessions");

        migrationBuilder.DropTable(
            name: "customers");

        migrationBuilder.DropTable(
            name: "users");
    }
}
