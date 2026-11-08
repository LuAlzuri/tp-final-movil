using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearTelefonoTutor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelefonosTutores",
                columns: table => new
                {
                    id_telefono = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tutor_id = table.Column<int>(type: "int", nullable: false),
                    nombre_prop = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    nro_tel = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelefonosTutores", x => x.id_telefono);
                    table.CheckConstraint("CK_TelefonosTutores_CamposObligatorios", "LEN(LTRIM(RTRIM([nombre_prop]))) > 0 AND LEN(LTRIM(RTRIM([nro_tel]))) > 0");
                    table.ForeignKey(
                        name: "FK_TelefonosTutores_Tutores_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "Tutores",
                        principalColumn: "id_tutor");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelefonosTutores_tutor_id_nro_tel",
                table: "TelefonosTutores",
                columns: new[] { "tutor_id", "nro_tel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelefonosTutores");
        }
    }
}
