using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CucineCRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiAccountCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "Utenti",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Utenti_ClienteId",
                table: "Utenti",
                column: "ClienteId",
                unique: true,
                filter: "\"ClienteId\" IS NOT NULL AND \"Eliminato\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Utenti_Clienti_ClienteId",
                table: "Utenti",
                column: "ClienteId",
                principalTable: "Clienti",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Utenti_Clienti_ClienteId",
                table: "Utenti");

            migrationBuilder.DropIndex(
                name: "IX_Utenti_ClienteId",
                table: "Utenti");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "Utenti");
        }
    }
}
