using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RedisRateLimiting.Sample.AspNetCore.Migrations
{
    /// <inheritdoc />
    public partial class DatabaseStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RateLimits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PermitLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    QueueLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    Window = table.Column<TimeSpan>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateLimits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Identifier = table.Column<string>(type: "TEXT", nullable: true),
                    RateLimitId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_RateLimits_RateLimitId",
                        column: x => x.RateLimitId,
                        principalTable: "RateLimits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "RateLimits",
                columns: new[] { "Id", "PermitLimit", "QueueLimit", "Window" },
                values: new object[,]
                {
                    { 1L, 1, 0, new TimeSpan(0, 0, 0, 1, 0) },
                    { 2L, 2, 0, new TimeSpan(0, 0, 0, 1, 0) },
                    { 3L, 3, 0, new TimeSpan(0, 0, 0, 1, 0) }
                });

            migrationBuilder.InsertData(
                table: "Clients",
                columns: new[] { "Id", "Identifier", "RateLimitId" },
                values: new object[,]
                {
                    { 1L, "client1", 1L },
                    { 2L, "client2", 2L },
                    { 3L, "client3", 3L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_RateLimitId",
                table: "Clients",
                column: "RateLimitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "RateLimits");
        }
    }
}
