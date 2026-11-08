using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearCupoTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CuposTurnos",
                columns: table => new
                {
                    cupo_id = table.Column<int>(type: "int", nullable: false),
                    turno_id = table.Column<int>(type: "int", nullable: false),
                    cantidad = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    observacion = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuposTurnos", x => new { x.cupo_id, x.turno_id });
                    table.CheckConstraint("CK_CuposTurnos_Cantidad", "[cantidad] >= 1");
                    table.ForeignKey(
                        name: "FK_CuposTurnos_CuposDiarios_cupo_id",
                        column: x => x.cupo_id,
                        principalTable: "CuposDiarios",
                        principalColumn: "id_cupo");
                    table.ForeignKey(
                        name: "FK_CuposTurnos_Turnos_turno_id",
                        column: x => x.turno_id,
                        principalTable: "Turnos",
                        principalColumn: "id_turno");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuposTurnos_turno_id",
                table: "CuposTurnos",
                column: "turno_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuposTurnos");
        }
    }
}
