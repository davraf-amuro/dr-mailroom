using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dr_mailroom_core.Infrastructure.Mailroom.Migrations;

/// <inheritdoc />
public partial class SenderOverviewIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message",
            columns: new[] { "MailboxId", "SenderAddress", "ReceivedAtUtc" })
            .Annotation("SqlServer:Include", new[] { "Subject" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Message_MailboxId_SenderAddress_ReceivedAtUtc",
            table: "Message");
    }
}
