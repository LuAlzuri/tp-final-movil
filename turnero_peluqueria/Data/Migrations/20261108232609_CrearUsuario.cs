using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace turnero_peluqueria.Data.Migrations
{
    /// <inheritdoc />
    public partial class CrearUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    Email = table.Column<string>(type: "varchar(254)", unicode: false, maxLength: 254, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    PasswordHash = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: false),
                    Nombre = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Rol = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.CheckConstraint("CK_Usuarios_CamposObligatorios", "LEN(LTRIM(RTRIM([Username]))) > 0 AND LEN(LTRIM(RTRIM([Email]))) > 0 AND LEN(LTRIM(RTRIM([Nombre]))) > 0 AND LEN(LTRIM(RTRIM([Apellido]))) > 0 AND LEN([PasswordHash]) > 0");
                    table.CheckConstraint("CK_Usuarios_Rol", "[Rol] IN ('Administrador', 'Veterinario', 'Tutor')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Username",
                table: "Usuarios",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
