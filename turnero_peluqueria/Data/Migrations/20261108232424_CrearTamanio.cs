using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearTamanio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tamanios",
                columns: table => new
                {
                    id_tamanio = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tamanio_descripcion = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tamanios", x => x.id_tamanio);
                    table.CheckConstraint("CK_Tamanios_Descripcion", "LEN(LTRIM(RTRIM([tamanio_descripcion]))) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tamanios_tamanio_descripcion",
                table: "Tamanios",
                column: "tamanio_descripcion",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tamanios");
        }
    }
}
