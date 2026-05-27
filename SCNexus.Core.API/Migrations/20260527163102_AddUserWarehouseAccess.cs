using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCNexus.Core.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUserWarehouseAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asp_net_user_warehouse_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    cod_almacen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    acceso_almacen = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modificado_por = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_asp_net_user_warehouse_access", x => x.id);
                    table.ForeignKey(
                        name: "f_k_asp_net_user_warehouse_access_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_asp_net_user_warehouse_access_user_id_cod_almacen",
                table: "asp_net_user_warehouse_access",
                columns: new[] { "user_id", "cod_almacen" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asp_net_user_warehouse_access");
        }
    }
}
