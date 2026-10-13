using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Conduit.Infrastructure.Migrations.SqlServer;

/// <inheritdoc />
public partial class AddBookmarks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ArticleBookmarks",
            columns: table => new
            {
                ArticleId = table.Column<int>(type: "int", nullable: false),
                PersonId = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArticleBookmarks", x => new { x.ArticleId, x.PersonId });
                table.ForeignKey(
                    name: "FK_ArticleBookmarks_Articles_ArticleId",
                    column: x => x.ArticleId,
                    principalTable: "Articles",
                    principalColumn: "ArticleId",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_ArticleBookmarks_Persons_PersonId",
                    column: x => x.PersonId,
                    principalTable: "Persons",
                    principalColumn: "PersonId",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_ArticleBookmarks_PersonId",
            table: "ArticleBookmarks",
            column: "PersonId"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ArticleBookmarks");
    }
}
