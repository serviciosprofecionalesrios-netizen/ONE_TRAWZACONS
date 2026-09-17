using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITServiceDeskApp.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddInventoryMasterArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryMasterArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Warehouse = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ItemType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Item = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PartNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExtraDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PurchaseUnitOfMeasure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Shelf = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SuggestedQuantity = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMasterArticles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMasterArticles_ProductCode",
                table: "InventoryMasterArticles",
                column: "ProductCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryMasterArticles");
        }
    }
}
