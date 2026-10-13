using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dr_mailroom_core.Infrastructure.Mailroom.Migrations;

/// <inheritdoc />
public partial class ContentArchive : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "TrashReceivedBeforeUtc",
            table: "Run",
            type: "datetime2",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ArchivedMessageContent",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MessageId = table.Column<long>(type: "bigint", nullable: false),
                RelativePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                ArchivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArchivedMessageContent", x => x.Id);
                table.ForeignKey(
                    name: "FK_ArchivedMessageContent_Message_MessageId",
                    column: x => x.MessageId,
                    principalTable: "Message",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ArchivedMessageContent_MessageId",
            table: "ArchivedMessageContent",
            column: "MessageId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ArchivedMessageContent");

        migrationBuilder.DropColumn(
            name: "TrashReceivedBeforeUtc",
            table: "Run");
    }
}
