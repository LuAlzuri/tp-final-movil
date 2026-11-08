using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearPerroPatologia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerrosPatologias",
                columns: table => new
                {
                    perro_id = table.Column<int>(type: "int", nullable: false),
                    patologia_id = table.Column<int>(type: "int", nullable: false),
                    controlado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    observacion = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerrosPatologias", x => new { x.perro_id, x.patologia_id });
                    table.ForeignKey(
                        name: "FK_PerrosPatologias_Patologias_patologia_id",
                        column: x => x.patologia_id,
                        principalTable: "Patologias",
                        principalColumn: "id_patologia");
                    table.ForeignKey(
                        name: "FK_PerrosPatologias_Perros_perro_id",
                        column: x => x.perro_id,
                        principalTable: "Perros",
                        principalColumn: "id_perro");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerrosPatologias_patologia_id",
                table: "PerrosPatologias",
                column: "patologia_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerrosPatologias");
        }
    }
}
