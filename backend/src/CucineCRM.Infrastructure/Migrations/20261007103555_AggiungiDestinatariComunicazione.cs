using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CucineCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiDestinatariComunicazione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Destinatari",
                table: "Comunicazioni",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // Le comunicazioni già pubblicate erano pensate per la rete vendita: restano visibili solo agli
                // agenti (la direzione può allargarle ai clienti dalla pagina Comunicazioni).
                defaultValue: "SoloAgenti");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Destinatari",
                table: "Comunicazioni");
        }
    }
}
