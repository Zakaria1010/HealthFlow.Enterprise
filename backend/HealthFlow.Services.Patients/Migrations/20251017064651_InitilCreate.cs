using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HealthFlow.Services.Patients.Migrations
{
    /// <inheritdoc />
    public partial class InitilCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MedicalRecordNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Patients",
                columns: new[] { "Id", "CreatedAt", "DateOfBirth", "FirstName", "LastName", "MedicalRecordNumber", "Status", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("0f327691-eb68-4de9-96b4-7c12928f7fd7"), new DateTime(2025, 10, 16, 6, 46, 51, 625, DateTimeKind.Utc).AddTicks(4360), new DateTime(1975, 6, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), "Jane", "Smith", "MRN002", "InTreatment", null },
                    { new Guid("38691ef6-b8a7-4a63-9576-46b1858801f2"), new DateTime(2025, 10, 17, 2, 46, 51, 625, DateTimeKind.Utc).AddTicks(5110), new DateTime(1990, 3, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), "Robert", "Johnson", "MRN003", "Critical", null },
                    { new Guid("76d735f9-d9a8-4d74-a15a-29abe043e66d"), new DateTime(2025, 10, 15, 6, 46, 51, 625, DateTimeKind.Utc).AddTicks(4350), new DateTime(1980, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "John", "Doe", "MRN001", "Admitted", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_CreatedAt",
                table: "Patients",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_LastName",
                table: "Patients",
                column: "LastName");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_MedicalRecordNumber",
                table: "Patients",
                column: "MedicalRecordNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Status",
                table: "Patients",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Patients");
        }
    }
}
