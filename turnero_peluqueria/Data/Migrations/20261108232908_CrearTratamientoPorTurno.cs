using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearTratamientoPorTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TratamientosPorTurno",
                columns: table => new
                {
                    turno_id = table.Column<int>(type: "int", nullable: false),
                    tratamiento_id = table.Column<int>(type: "int", nullable: false),
                    razon = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TratamientosPorTurno", x => new { x.turno_id, x.tratamiento_id });
                    table.ForeignKey(
                        name: "FK_TratamientosPorTurno_Tratamientos_tratamiento_id",
                        column: x => x.tratamiento_id,
                        principalTable: "Tratamientos",
                        principalColumn: "id_tratamiento");
                    table.ForeignKey(
                        name: "FK_TratamientosPorTurno_Turnos_turno_id",
                        column: x => x.turno_id,
                        principalTable: "Turnos",
                        principalColumn: "id_turno");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosPorTurno_tratamiento_id",
                table: "TratamientosPorTurno",
                column: "tratamiento_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TratamientosPorTurno");
        }
    }
}
