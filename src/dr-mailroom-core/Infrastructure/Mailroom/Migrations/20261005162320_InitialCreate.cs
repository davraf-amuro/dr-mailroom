using System;
using dr_mailroom_core.Infrastructure.Mailroom;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dr_mailroom_core.Infrastructure.Mailroom.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Mailbox",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                EmailAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                EncryptedRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                HistoryId = table.Column<decimal>(type: "decimal(20,0)", nullable: true),
                BackfillPageToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                BackfillCompleted = table.Column<bool>(type: "bit", nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Mailbox", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Filter",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MailboxId = table.Column<int>(type: "int", nullable: true),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SenderAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                SubjectMatchType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                SubjectValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                AttachmentSubfolder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Filter", x => x.Id);
                table.CheckConstraint("CK_Filter_Subject", "([SubjectMatchType] IS NULL AND [SubjectValue] IS NULL) OR ([SubjectMatchType] IS NOT NULL AND [SubjectValue] IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_Filter_Mailbox_MailboxId",
                    column: x => x.MailboxId,
                    principalTable: "Mailbox",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "Message",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MailboxId = table.Column<int>(type: "int", nullable: false),
                GmailMessageId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ThreadId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                SenderAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                SenderName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                Subject = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                YearMonth = table.Column<int>(type: "int", nullable: false, computedColumnSql: "YEAR([ReceivedAtUtc]) * 100 + MONTH([ReceivedAtUtc])", stored: true),
                Labels = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                HasAttachments = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Message", x => x.Id);
                table.ForeignKey(
                    name: "FK_Message_Mailbox_MailboxId",
                    column: x => x.MailboxId,
                    principalTable: "Mailbox",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "FilterProcess",
            columns: table => new
            {
                FilterId = table.Column<int>(type: "int", nullable: false),
                ProcessType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FilterProcess", x => new { x.FilterId, x.ProcessType });
                table.ForeignKey(
                    name: "FK_FilterProcess_Filter_FilterId",
                    column: x => x.FilterId,
                    principalTable: "Filter",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Run",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MailboxId = table.Column<int>(type: "int", nullable: false),
                Trigger = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                FilterId = table.Column<int>(type: "int", nullable: true),
                RequestedProcesses = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                MessagesRead = table.Column<int>(type: "int", nullable: false),
                MessagesProcessed = table.Column<int>(type: "int", nullable: false),
                ErrorCount = table.Column<int>(type: "int", nullable: false),
                ErrorSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Run", x => x.Id);
                table.ForeignKey(
                    name: "FK_Run_Filter_FilterId",
                    column: x => x.FilterId,
                    principalTable: "Filter",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Run_Mailbox_MailboxId",
                    column: x => x.MailboxId,
                    principalTable: "Mailbox",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Alert",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MessageId = table.Column<long>(type: "bigint", nullable: false),
                SenderAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                Subject = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Alert", x => x.Id);
                table.ForeignKey(
                    name: "FK_Alert_Message_MessageId",
                    column: x => x.MessageId,
                    principalTable: "Message",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ExtractedAttachment",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MessageId = table.Column<long>(type: "bigint", nullable: false),
                PartId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                RelativePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                SavedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExtractedAttachment", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExtractedAttachment_Message_MessageId",
                    column: x => x.MessageId,
                    principalTable: "Message",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProcessExecution",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MessageId = table.Column<long>(type: "bigint", nullable: false),
                ProcessType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProcessExecution", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProcessExecution_Message_MessageId",
                    column: x => x.MessageId,
                    principalTable: "Message",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AlertFilter",
            columns: table => new
            {
                AlertId = table.Column<long>(type: "bigint", nullable: false),
                FilterId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AlertFilter", x => new { x.AlertId, x.FilterId });
                table.ForeignKey(
                    name: "FK_AlertFilter_Alert_AlertId",
                    column: x => x.AlertId,
                    principalTable: "Alert",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AlertFilter_Filter_FilterId",
                    column: x => x.FilterId,
                    principalTable: "Filter",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Alert_ConsumedAtUtc",
            table: "Alert",
            column: "ConsumedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_Alert_MessageId",
            table: "Alert",
            column: "MessageId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AlertFilter_FilterId",
            table: "AlertFilter",
            column: "FilterId");

        migrationBuilder.CreateIndex(
            name: "IX_ExtractedAttachment_MessageId_PartId",
            table: "ExtractedAttachment",
            columns: new[] { "MessageId", "PartId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Filter_MailboxId",
            table: "Filter",
            column: "MailboxId");

        migrationBuilder.CreateIndex(
            name: "IX_Filter_SenderAddress",
            table: "Filter",
            column: "SenderAddress");

        migrationBuilder.CreateIndex(
            name: "IX_Mailbox_EmailAddress",
            table: "Mailbox",
            column: "EmailAddress",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Message_MailboxId_GmailMessageId",
            table: "Message",
            columns: new[] { "MailboxId", "GmailMessageId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Message_MailboxId_SenderAddress_YearMonth",
            table: "Message",
            columns: new[] { "MailboxId", "SenderAddress", "YearMonth" });

        migrationBuilder.CreateIndex(
            name: "IX_ProcessExecution_MessageId_ProcessType",
            table: "ProcessExecution",
            columns: new[] { "MessageId", "ProcessType" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Run_FilterId",
            table: "Run",
            column: "FilterId");

        migrationBuilder.CreateIndex(
            name: "IX_Run_MailboxId_StartedAtUtc",
            table: "Run",
            columns: new[] { "MailboxId", "StartedAtUtc" });

        // Viste statistiche (T5): conteggi su tutte le mail lette (D9), lette da EF come entità keyless
        migrationBuilder.Sql($"""
            CREATE VIEW [{MailroomViews.SenderStats}] AS
            SELECT [MailboxId], [SenderAddress], CAST(COUNT_BIG(*) AS int) AS [MailCount]
            FROM [Message]
            GROUP BY [MailboxId], [SenderAddress]
            """);

        migrationBuilder.Sql($"""
            CREATE VIEW [{MailroomViews.SenderMonthlyStats}] AS
            SELECT [MailboxId], [SenderAddress], [YearMonth], CAST(COUNT_BIG(*) AS int) AS [MailCount]
            FROM [Message]
            GROUP BY [MailboxId], [SenderAddress], [YearMonth]
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Le viste dipendono da Message: vanno eliminate prima delle tabelle
        migrationBuilder.Sql($"DROP VIEW IF EXISTS [{MailroomViews.SenderMonthlyStats}]");
        migrationBuilder.Sql($"DROP VIEW IF EXISTS [{MailroomViews.SenderStats}]");

        migrationBuilder.DropTable(
            name: "AlertFilter");

        migrationBuilder.DropTable(
            name: "ExtractedAttachment");

        migrationBuilder.DropTable(
            name: "FilterProcess");

        migrationBuilder.DropTable(
            name: "ProcessExecution");

        migrationBuilder.DropTable(
            name: "Run");

        migrationBuilder.DropTable(
            name: "Alert");

        migrationBuilder.DropTable(
            name: "Filter");

        migrationBuilder.DropTable(
            name: "Message");

        migrationBuilder.DropTable(
            name: "Mailbox");
    }
}
