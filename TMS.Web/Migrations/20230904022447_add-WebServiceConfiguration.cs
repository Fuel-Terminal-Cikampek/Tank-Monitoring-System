using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CSL.Web.Migrations
{
    public partial class addWebServiceConfiguration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Service_Configuration",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    service_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<bool>(type: "bit", nullable: false),
                    url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Create_Timestamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Create_By = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Update_Timestamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Update_By = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Service_Configuration", x => x.id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Service_Configuration");
        }
    }
}
