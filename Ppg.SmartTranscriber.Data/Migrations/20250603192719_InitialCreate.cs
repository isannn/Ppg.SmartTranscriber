using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace Ppg.SmartTranscriber.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.CreateTable(
                name: "TranscriptionJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TranscriptText = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptionJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TranscriptionSegments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<double>(type: "double precision", nullable: false),
                    EndTime = table.Column<double>(type: "double precision", nullable: false),
                    Speaker = table.Column<string>(type: "text", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    TextVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "Text" })
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptionSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranscriptionSegments_TranscriptionJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "TranscriptionJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptionSegments_JobId",
                table: "TranscriptionSegments",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptionSegments_TextVector",
                table: "TranscriptionSegments",
                column: "TextVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranscriptionSegments");

            migrationBuilder.DropTable(
                name: "TranscriptionJobs");
        }
    }
}
