using dr_mailroom_core.Gmail;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dr_mailroom_core.Infrastructure.Mailroom.Migrations;

/// <inheritdoc />
public partial class MessageIsSent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message");

        migrationBuilder.AddColumn<bool>(
            name: "IsSent",
            table: "Message",
            type: "bit",
            nullable: false,
            defaultValue: false);

        // Pregresso dal DB, senza rileggere Gmail: stesso criterio dell'ingestione (label SENT o indirizzo della casella)
        migrationBuilder.Sql($"""
            UPDATE m SET m.[IsSent] = 1
            FROM [Message] m
            INNER JOIN [Mailbox] b ON b.[Id] = m.[MailboxId]
            WHERE (',' + m.[Labels] + ',') LIKE '%,{GmailConstants.SentLabel},%'
               OR LOWER(m.[SenderAddress]) = LOWER(b.[EmailAddress])
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message",
            columns: new[] { "MailboxId", "SenderAddress", "ReceivedAtUtc" })
            .Annotation("SqlServer:Include", new[] { "Subject", "IsSent" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message");

        migrationBuilder.DropColumn(
            name: "IsSent",
            table: "Message");

        migrationBuilder.CreateIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message",
            columns: new[] { "MailboxId", "SenderAddress", "ReceivedAtUtc" })
            .Annotation("SqlServer:Include", new[] { "Subject" });
    }
}
