using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCNexus.Core.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBodegaCentralToWarehouseGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bodega_central",
                table: "asp_net_warehouse_group",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bodega_central",
                table: "asp_net_warehouse_group");
        }
    }
}
