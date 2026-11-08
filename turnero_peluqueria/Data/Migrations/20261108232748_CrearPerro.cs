using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearPerro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Perros",
                columns: table => new
                {
                    id_perro = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tutor_id = table.Column<int>(type: "int", nullable: false),
                    nombre = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    fecha_nacimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    reactivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    cal_vac_com = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    grupo_etario_id = table.Column<int>(type: "int", nullable: false),
                    tamanio_id = table.Column<int>(type: "int", nullable: false),
                    eliminado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Perros", x => x.id_perro);
                    table.CheckConstraint("CK_Perros_Nombre", "LEN(LTRIM(RTRIM([nombre]))) > 0");
                    table.ForeignKey(
                        name: "FK_Perros_GruposEtarios_grupo_etario_id",
                        column: x => x.grupo_etario_id,
                        principalTable: "GruposEtarios",
                        principalColumn: "id_grupo_etario");
                    table.ForeignKey(
                        name: "FK_Perros_Tamanios_tamanio_id",
                        column: x => x.tamanio_id,
                        principalTable: "Tamanios",
                        principalColumn: "id_tamanio");
                    table.ForeignKey(
                        name: "FK_Perros_Tutores_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "Tutores",
                        principalColumn: "id_tutor");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Perros_grupo_etario_id",
                table: "Perros",
                column: "grupo_etario_id");

            migrationBuilder.CreateIndex(
                name: "IX_Perros_tamanio_id",
                table: "Perros",
                column: "tamanio_id");

            migrationBuilder.CreateIndex(
                name: "IX_Perros_tutor_id",
                table: "Perros",
                column: "tutor_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Perros");
        }
    }
}
