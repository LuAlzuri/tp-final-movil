using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearImagen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Imagenes",
                columns: table => new
                {
                    id_imagen = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    perro_id = table.Column<int>(type: "int", nullable: false),
                    nombre_archivo = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    ruta_relativa = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    tipo_contenido = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Imagenes", x => x.id_imagen);
                    table.CheckConstraint("CK_Imagenes_Tamano", "[tamano_bytes] > 0");
                    table.ForeignKey(
                        name: "FK_Imagenes_Perros_perro_id",
                        column: x => x.perro_id,
                        principalTable: "Perros",
                        principalColumn: "id_perro",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Imagenes_perro_id",
                table: "Imagenes",
                column: "perro_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Imagenes");
        }
    }
}
