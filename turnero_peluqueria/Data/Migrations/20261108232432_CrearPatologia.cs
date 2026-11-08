using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearPatologia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Patologias",
                columns: table => new
                {
                    id_patologia = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    patologia_descripcion = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patologias", x => x.id_patologia);
                    table.CheckConstraint("CK_Patologias_Descripcion", "LEN(LTRIM(RTRIM([patologia_descripcion]))) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patologias_patologia_descripcion",
                table: "Patologias",
                column: "patologia_descripcion",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Patologias");
        }
    }
}
