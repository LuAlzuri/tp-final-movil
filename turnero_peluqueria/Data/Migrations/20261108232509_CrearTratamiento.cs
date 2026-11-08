using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearTratamiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tratamientos",
                columns: table => new
                {
                    id_tratamiento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tratamiento_descripcion = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tratamientos", x => x.id_tratamiento);
                    table.CheckConstraint("CK_Tratamientos_Descripcion", "LEN(LTRIM(RTRIM([tratamiento_descripcion]))) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tratamientos_tratamiento_descripcion",
                table: "Tratamientos",
                column: "tratamiento_descripcion",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tratamientos");
        }
    }
}
