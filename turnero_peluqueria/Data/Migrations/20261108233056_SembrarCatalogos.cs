using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class SembrarCatalogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Estados",
                columns: new[] { "id_estado", "estado_descripcion" },
                values: new object[,]
                {
                    { 1, "Pendiente" },
                    { 2, "Aprobado" },
                    { 3, "Rechazado" },
                    { 4, "Cancelado por el tutor" },
                    { 5, "Cancelado por el veterinario" },
                    { 6, "Completado" }
                });

            migrationBuilder.InsertData(
                table: "GruposEtarios",
                columns: new[] { "id_grupo_etario", "grupo_descripcion" },
                values: new object[,]
                {
                    { 1, "Cachorro" },
                    { 2, "Adulto" },
                    { 3, "Senior" }
                });

            migrationBuilder.InsertData(
                table: "Tamanios",
                columns: new[] { "id_tamanio", "tamanio_descripcion" },
                values: new object[,]
                {
                    { 1, "Pequeño" },
                    { 2, "Mediano" },
                    { 3, "Grande" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Estados",
                keyColumn: "id_estado",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Estados",
                keyColumn: "id_estado",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Estados",
                keyColumn: "id_estado",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Estados",
                keyColumn: "id_estado",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Estados",
                keyColumn: "id_estado",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Estados",
                keyColumn: "id_estado",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "GruposEtarios",
                keyColumn: "id_grupo_etario",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "GruposEtarios",
                keyColumn: "id_grupo_etario",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "GruposEtarios",
                keyColumn: "id_grupo_etario",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Tamanios",
                keyColumn: "id_tamanio",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Tamanios",
                keyColumn: "id_tamanio",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Tamanios",
                keyColumn: "id_tamanio",
                keyValue: 3);
        }
    }
}
