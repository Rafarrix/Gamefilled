using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gamefilled.V2.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameLibraryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IgdbGameId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    ReviewText = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    ContainsSpoilers = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameLibraryEntries", x => x.Id);
                    table.CheckConstraint("CK_GameLibraryEntries_IgdbGameId", "[IgdbGameId] > 0");
                    table.CheckConstraint("CK_GameLibraryEntries_Rating", "[Rating] IS NULL OR [Rating] BETWEEN 1 AND 10");
                    table.CheckConstraint("CK_GameLibraryEntries_ReviewLength", "[ReviewText] IS NULL OR LEN([ReviewText]) <= 5000");
                    table.CheckConstraint("CK_GameLibraryEntries_Status", "[Status] IN (N'played', N'playing', N'backlog', N'wishlist')");
                    table.ForeignKey(
                        name: "FK_GameLibraryEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameLibraryEntries_UserId_UpdatedAtUtc",
                table: "GameLibraryEntries",
                columns: new[] { "UserId", "UpdatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UX_GameLibraryEntries_UserId_IgdbGameId",
                table: "GameLibraryEntries",
                columns: new[] { "UserId", "IgdbGameId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameLibraryEntries");
        }
    }
}
