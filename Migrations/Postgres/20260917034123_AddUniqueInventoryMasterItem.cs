using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddUniqueInventoryMasterItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_InventoryMasterArticles_Item",
                table: "InventoryMasterArticles",
                column: "Item",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryMasterArticles_Item",
                table: "InventoryMasterArticles");
        }
    }
}
