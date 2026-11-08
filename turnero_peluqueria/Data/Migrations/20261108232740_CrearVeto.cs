using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearVeto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vetos",
                columns: table => new
                {
                    id_veto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tutor_id = table.Column<int>(type: "int", nullable: false),
                    veterinario_id = table.Column<int>(type: "int", nullable: true),
                    fecha_inicio = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    motivo = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    fecha_fin = table.Column<DateTime>(type: "datetime", nullable: true),
                    motivo_fin_veto = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vetos", x => x.id_veto);
                    table.CheckConstraint("CK_Vetos_Automatico", "[veterinario_id] IS NOT NULL OR ([motivo] IS NOT NULL AND [motivo] = 'Superó número de cancelaciones')");
                    table.CheckConstraint("CK_Vetos_Fechas", "[fecha_fin] IS NULL OR [fecha_fin] >= [fecha_inicio]");
                    table.CheckConstraint("CK_Vetos_MotivoFin", "[fecha_fin] IS NOT NULL OR [motivo_fin_veto] IS NULL");
                    table.ForeignKey(
                        name: "FK_Vetos_Tutores_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "Tutores",
                        principalColumn: "id_tutor");
                    table.ForeignKey(
                        name: "FK_Vetos_Veterinarios_veterinario_id",
                        column: x => x.veterinario_id,
                        principalTable: "Veterinarios",
                        principalColumn: "id_veterinario");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vetos_tutor_id",
                table: "Vetos",
                column: "tutor_id",
                unique: true,
                filter: "[fecha_fin] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Vetos_veterinario_id",
                table: "Vetos",
                column: "veterinario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vetos");
        }
    }
}
