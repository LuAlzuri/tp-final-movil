using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearAuditoriaAccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditoriaAcciones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    UsuarioEmail = table.Column<string>(type: "varchar(254)", unicode: false, maxLength: 254, nullable: true),
                    Accion = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Entidad = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EntidadId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Ip = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    Observacion = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaAcciones", x => x.Id);
                    table.CheckConstraint("CK_AuditoriaAcciones_CamposObligatorios", "LEN(LTRIM(RTRIM([Accion]))) > 0 AND LEN(LTRIM(RTRIM([Entidad]))) > 0");
                    table.ForeignKey(
                        name: "FK_AuditoriaAcciones_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaAcciones_Entidad_EntidadId",
                table: "AuditoriaAcciones",
                columns: new[] { "Entidad", "EntidadId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaAcciones_FechaUtc_Id",
                table: "AuditoriaAcciones",
                columns: new[] { "FechaUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaAcciones_UsuarioId",
                table: "AuditoriaAcciones",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriaAcciones");
        }
    }
}
