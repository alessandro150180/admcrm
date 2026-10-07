using CucineCRM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CucineCRM.Infrastructure.Persistence.Configurations;

public class ComunicazioneConfiguration : IEntityTypeConfiguration<Comunicazione>
{
    public void Configure(EntityTypeBuilder<Comunicazione> builder)
    {
        builder.ToTable("Comunicazioni");
        builder.Property(c => c.Titolo).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Descrizione).HasMaxLength(2000);
        builder.Property(c => c.NomeFile).IsRequired().HasMaxLength(260);
        builder.Property(c => c.TipoContenuto).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Destinatari).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(c => c.UtentePubblicazione)
            .WithMany(u => u.Comunicazioni)
            .HasForeignKey(c => c.UtentePubblicazioneId)
            .OnDelete(DeleteBehavior.Restrict);

        // Elenco ordinato per data di pubblicazione più recente: indice utile alla query principale.
        builder.HasIndex(c => c.DataCreazione);
    }
}
