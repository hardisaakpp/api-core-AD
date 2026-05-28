using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCNexus.Core.API.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseGroupIdToUserAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "warehouse_group_id",
                table: "asp_net_user_warehouse_access",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_asp_net_user_warehouse_access_warehouse_group_id",
                table: "asp_net_user_warehouse_access",
                column: "warehouse_group_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_asp_net_user_warehouse_access_asp_net_warehouse_group_warehouse_group_id",
                table: "asp_net_user_warehouse_access",
                column: "warehouse_group_id",
                principalTable: "asp_net_warehouse_group",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_asp_net_user_warehouse_access_asp_net_warehouse_group_warehouse_group_id",
                table: "asp_net_user_warehouse_access");

            migrationBuilder.DropIndex(
                name: "i_x_asp_net_user_warehouse_access_warehouse_group_id",
                table: "asp_net_user_warehouse_access");

            migrationBuilder.DropColumn(
                name: "warehouse_group_id",
                table: "asp_net_user_warehouse_access");
        }
    }
}
