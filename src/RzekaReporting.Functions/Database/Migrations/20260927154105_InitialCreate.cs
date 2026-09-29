using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RzekaReporting.Functions.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Issues",
                columns: table => new
                {
                    Fingerprint = table.Column<string>(
                        type: "nchar(64)",
                        fixedLength: true,
                        maxLength: 64,
                        nullable: false
                    ),
                    Canonical = table.Column<string>(
                        type: "nvarchar(4000)",
                        maxLength: 4000,
                        nullable: false
                    ),
                    Count = table.Column<int>(type: "int", nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Fingerprint);
                }
            );

            migrationBuilder.CreateTable(
                name: "CrashReports",
                columns: table => new
                {
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(
                        type: "nchar(64)",
                        fixedLength: true,
                        maxLength: 64,
                        nullable: false
                    ),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppName = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    AppVersion = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    ExceptionMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrashReports", x => x.ReportId);
                    table.ForeignKey(
                        name: "FK_CrashReports_Issues_Fingerprint",
                        column: x => x.Fingerprint,
                        principalTable: "Issues",
                        principalColumn: "Fingerprint",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CrashReports_Fingerprint",
                table: "CrashReports",
                column: "Fingerprint"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CrashReports");

            migrationBuilder.DropTable(name: "Issues");
        }
    }
}
