using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearGrupoEtario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GruposEtarios",
                columns: table => new
                {
                    id_grupo_etario = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    grupo_descripcion = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposEtarios", x => x.id_grupo_etario);
                    table.CheckConstraint("CK_GruposEtarios_Descripcion", "LEN(LTRIM(RTRIM([grupo_descripcion]))) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GruposEtarios_grupo_descripcion",
                table: "GruposEtarios",
                column: "grupo_descripcion",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GruposEtarios");
        }
    }
}
