using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearServicioPorTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiciosPorTurno",
                columns: table => new
                {
                    turno_id = table.Column<int>(type: "int", nullable: false),
                    servicio_id = table.Column<int>(type: "int", nullable: false),
                    completado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiciosPorTurno", x => new { x.turno_id, x.servicio_id });
                    table.ForeignKey(
                        name: "FK_ServiciosPorTurno_Servicios_servicio_id",
                        column: x => x.servicio_id,
                        principalTable: "Servicios",
                        principalColumn: "id_servicio");
                    table.ForeignKey(
                        name: "FK_ServiciosPorTurno_Turnos_turno_id",
                        column: x => x.turno_id,
                        principalTable: "Turnos",
                        principalColumn: "id_turno");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiciosPorTurno_servicio_id",
                table: "ServiciosPorTurno",
                column: "servicio_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiciosPorTurno");
        }
    }
}
