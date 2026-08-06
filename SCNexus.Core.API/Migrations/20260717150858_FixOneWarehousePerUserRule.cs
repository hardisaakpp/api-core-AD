using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCNexus.Core.API.Migrations
{
    /// <inheritdoc />
    public partial class FixOneWarehousePerUserRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_asp_net_user_warehouse_access_user_id",
                table: "asp_net_user_warehouse_access");

            migrationBuilder.CreateIndex(
                name: "i_x_asp_net_user_warehouse_access_user_id_cod_almacen_warehouse_group_id",
                table: "asp_net_user_warehouse_access",
                columns: new[] { "user_id", "cod_almacen", "warehouse_group_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_asp_net_user_warehouse_access_user_id_cod_almacen_warehouse_group_id",
                table: "asp_net_user_warehouse_access");

            migrationBuilder.CreateIndex(
                name: "i_x_asp_net_user_warehouse_access_user_id",
                table: "asp_net_user_warehouse_access",
                column: "user_id",
                unique: true);
        }
    }
}
