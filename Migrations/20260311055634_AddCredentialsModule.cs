using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDeskApp.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Credentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EnvironmentOrLocation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AccessUrlOrHost = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Port = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Secret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credentials", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_Name_Type",
                table: "Credentials",
                columns: new[] { "Name", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Credentials");
        }
    }
}
