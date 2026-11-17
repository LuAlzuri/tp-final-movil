using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegistrarOrigenPresencialTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsPresencial",
                table: "Turnos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // EXEC demora la compilación del UPDATE hasta que exista la columna,
            // también al ejecutar el script idempotente en SSMS.
            migrationBuilder.Sql("""
                EXEC(N'
                UPDATE t SET [EsPresencial] = 1
                FROM [Turnos] AS t
                WHERE EXISTS (
                    SELECT 1 FROM [AuditoriaAcciones] AS a
                    WHERE a.[Entidad] = N''Turno'' AND a.[Accion] = N''SolicitarPresencial''
                      AND a.[EntidadId] = CONVERT(nvarchar(100), t.[id_turno])
                );
                ');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsPresencial",
                table: "Turnos");
        }
    }
}
