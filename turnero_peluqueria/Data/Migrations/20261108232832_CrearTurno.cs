using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Turnos",
                columns: table => new
                {
                    id_turno = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    perro_id = table.Column<int>(type: "int", nullable: false),
                    estado_id = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    veterinario_id = table.Column<int>(type: "int", nullable: true),
                    observaciones = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: true),
                    fecha_solicitud = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    fecha_estado = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turnos", x => x.id_turno);
                    table.CheckConstraint("CK_Turnos_Fechas", "[fecha_estado] >= [fecha_solicitud]");
                    table.CheckConstraint("CK_Turnos_Veterinario", "[estado_id] NOT IN (2, 5, 6) OR [veterinario_id] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Turnos_Estados_estado_id",
                        column: x => x.estado_id,
                        principalTable: "Estados",
                        principalColumn: "id_estado");
                    table.ForeignKey(
                        name: "FK_Turnos_Perros_perro_id",
                        column: x => x.perro_id,
                        principalTable: "Perros",
                        principalColumn: "id_perro");
                    table.ForeignKey(
                        name: "FK_Turnos_Veterinarios_veterinario_id",
                        column: x => x.veterinario_id,
                        principalTable: "Veterinarios",
                        principalColumn: "id_veterinario");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_estado_id_fecha_estado",
                table: "Turnos",
                columns: new[] { "estado_id", "fecha_estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_fecha_solicitud",
                table: "Turnos",
                column: "fecha_solicitud");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_perro_id",
                table: "Turnos",
                column: "perro_id");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_veterinario_id",
                table: "Turnos",
                column: "veterinario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Turnos");
        }
    }
}
