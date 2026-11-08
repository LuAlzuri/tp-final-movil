using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearCupoDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CuposDiarios",
                columns: table => new
                {
                    id_cupo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fecha_cupo = table.Column<DateOnly>(type: "date", nullable: false),
                    nro_cupo_dis = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuposDiarios", x => x.id_cupo);
                    table.CheckConstraint("CK_CuposDiarios_Total", "[nro_cupo_dis] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuposDiarios_fecha_cupo",
                table: "CuposDiarios",
                column: "fecha_cupo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuposDiarios");
        }
    }
}
