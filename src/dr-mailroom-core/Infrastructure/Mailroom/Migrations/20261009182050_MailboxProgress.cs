using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dr_mailroom_core.Infrastructure.Mailroom.Migrations;

/// <inheritdoc />
public partial class MailboxProgress : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "LastErrorRunId",
            table: "Mailbox",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "MessagesTotal",
            table: "Mailbox",
            type: "int",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastErrorRunId",
            table: "Mailbox");

        migrationBuilder.DropColumn(
            name: "MessagesTotal",
            table: "Mailbox");
    }
}
