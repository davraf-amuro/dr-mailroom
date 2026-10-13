using System;
using dr_mailroom_core.Infrastructure.Mailroom;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dr_mailroom_core.Infrastructure.Mailroom.Migrations;

/// <inheritdoc />
public partial class TrashSender : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message");

        migrationBuilder.AddColumn<string>(
            name: "SenderAddress",
            table: "Run",
            type: "nvarchar(320)",
            maxLength: 320,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "TrashedAtUtc",
            table: "Message",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EncryptedTrashRefreshToken",
            table: "Mailbox",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message",
            columns: new[] { "MailboxId", "SenderAddress", "ReceivedAtUtc" })
            .Annotation("SqlServer:Include", new[] { "Subject", "IsSent", "TrashedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Message_TrashedAtUtc",
            table: "Message",
            column: "TrashedAtUtc",
            filter: "[TrashedAtUtc] IS NOT NULL");

        // Le statistiche per mittente non contano le mail spostate nel cestino da dr-mailroom
        migrationBuilder.Sql($"""
            ALTER VIEW [{MailroomViews.SenderStats}] AS
            SELECT [MailboxId], [SenderAddress], CAST(COUNT_BIG(*) AS int) AS [MailCount]
            FROM [Message]
            WHERE [TrashedAtUtc] IS NULL
            GROUP BY [MailboxId], [SenderAddress]
            """);

        migrationBuilder.Sql($"""
            ALTER VIEW [{MailroomViews.SenderMonthlyStats}] AS
            SELECT [MailboxId], [SenderAddress], [YearMonth], CAST(COUNT_BIG(*) AS int) AS [MailCount]
            FROM [Message]
            WHERE [TrashedAtUtc] IS NULL
            GROUP BY [MailboxId], [SenderAddress], [YearMonth]
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Le viste leggono TrashedAtUtc: tornano alla definizione iniziale prima di eliminare la colonna
        migrationBuilder.Sql($"""
            ALTER VIEW [{MailroomViews.SenderStats}] AS
            SELECT [MailboxId], [SenderAddress], CAST(COUNT_BIG(*) AS int) AS [MailCount]
            FROM [Message]
            GROUP BY [MailboxId], [SenderAddress]
            """);

        migrationBuilder.Sql($"""
            ALTER VIEW [{MailroomViews.SenderMonthlyStats}] AS
            SELECT [MailboxId], [SenderAddress], [YearMonth], CAST(COUNT_BIG(*) AS int) AS [MailCount]
            FROM [Message]
            GROUP BY [MailboxId], [SenderAddress], [YearMonth]
            """);

        migrationBuilder.DropIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message");

        migrationBuilder.DropIndex(
            name: "IX_Message_TrashedAtUtc",
            table: "Message");

        migrationBuilder.DropColumn(
            name: "SenderAddress",
            table: "Run");

        migrationBuilder.DropColumn(
            name: "TrashedAtUtc",
            table: "Message");

        migrationBuilder.DropColumn(
            name: "EncryptedTrashRefreshToken",
            table: "Mailbox");

        migrationBuilder.CreateIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message",
            columns: new[] { "MailboxId", "SenderAddress", "ReceivedAtUtc" })
            .Annotation("SqlServer:Include", new[] { "Subject", "IsSent" });
    }
}
