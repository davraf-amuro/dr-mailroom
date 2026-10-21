using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dr_mailroom_core.Infrastructure.Mailroom.Migrations;

/// <inheritdoc />
public partial class TrashWithoutAttachments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message");

        migrationBuilder.AddColumn<bool>(
            name: "TrashWithoutAttachmentsOnly",
            table: "Run",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message",
            columns: new[] { "MailboxId", "SenderAddress", "ReceivedAtUtc" })
            .Annotation("SqlServer:Include", new[] { "Subject", "IsSent", "HasAttachments", "TrashedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message");

        migrationBuilder.DropColumn(
            name: "TrashWithoutAttachmentsOnly",
            table: "Run");

        migrationBuilder.CreateIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message",
            columns: new[] { "MailboxId", "SenderAddress", "ReceivedAtUtc" })
            .Annotation("SqlServer:Include", new[] { "Subject", "IsSent", "TrashedAtUtc" });
    }
}
