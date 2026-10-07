using CucineCRM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CucineCRM.Infrastructure.Persistence.Configurations;

public class UtenteConfiguration : IEntityTypeConfiguration<Utente>
{
    public void Configure(EntityTypeBuilder<Utente> builder)
    {
        builder.ToTable("Utenti");

        builder.Property(u => u.Nome).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Cognome).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Ruolo).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasOne(u => u.Agente)
            .WithMany()
            .HasForeignKey(u => u.AgenteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(u => u.Cliente)
            .WithMany()
            .HasForeignKey(u => u.ClienteId)
            .OnDelete(DeleteBehavior.SetNull);

        // Un cliente ha al massimo un account attivo
        builder.HasIndex(u => u.ClienteId).IsUnique().HasFilter("\"ClienteId\" IS NOT NULL AND \"Eliminato\" = false");

        // Indice per velocizzare i filtri per ruolo (usati spesso nello scoping dei permessi)
        builder.HasIndex(u => u.Ruolo);
    }
}
