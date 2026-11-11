using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureProxy.Dal.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigurationChangeLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationChangeLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Destinations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TimeoutMs = table.Column<int>(type: "int", nullable: false),
                    AllowedPorts = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AuthProfileRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Destinations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncryptionProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Algorithm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    KeyRef = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncryptionProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DestinationId = table.Column<int>(type: "int", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ForwardingOccurred = table.Column<bool>(type: "bit", nullable: false),
                    UpstreamStatusCode = table.Column<int>(type: "int", nullable: true),
                    ProxyStatusCode = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Operations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DestinationId = table.Column<int>(type: "int", nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RelativePathTemplate = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxRequestBytes = table.Column<long>(type: "bigint", nullable: false),
                    MaxResponseBytes = table.Column<long>(type: "bigint", nullable: false),
                    EncryptionRequired = table.Column<bool>(type: "bit", nullable: false),
                    EncryptionProfileId = table.Column<int>(type: "int", nullable: true),
                    RejectUnknownParameters = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Operations_Destinations_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "Destinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Operations_EncryptionProfiles_EncryptionProfileId",
                        column: x => x.EncryptionProfileId,
                        principalTable: "EncryptionProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HeaderRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<int>(type: "int", nullable: false),
                    HeaderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeaderRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeaderRules_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParameterRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<int>(type: "int", nullable: false),
                    MaxLength = table.Column<int>(type: "int", nullable: true),
                    MinValue = table.Column<long>(type: "bigint", nullable: true),
                    MaxValue = table.Column<long>(type: "bigint", nullable: true),
                    Pattern = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParameterRules_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Destinations_Name",
                table: "Destinations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EncryptionProfiles_Name",
                table: "EncryptionProfiles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeaderRules_OperationId_HeaderName",
                table: "HeaderRules",
                columns: new[] { "OperationId", "HeaderName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Operations_DestinationId",
                table: "Operations",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_Operations_EncryptionProfileId",
                table: "Operations",
                column: "EncryptionProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Operations_OperationKey",
                table: "Operations",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterRules_OperationId_Name",
                table: "ParameterRules",
                columns: new[] { "OperationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_CorrelationId",
                table: "RequestLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_StartedAtUtc",
                table: "RequestLogs",
                column: "StartedAtUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigurationChangeLogs");

            migrationBuilder.DropTable(
                name: "HeaderRules");

            migrationBuilder.DropTable(
                name: "ParameterRules");

            migrationBuilder.DropTable(
                name: "RequestLogs");

            migrationBuilder.DropTable(
                name: "Operations");

            migrationBuilder.DropTable(
                name: "Destinations");

            migrationBuilder.DropTable(
                name: "EncryptionProfiles");
        }
    }
}
