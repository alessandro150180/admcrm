using CucineCRM.Domain.Enums;

namespace CucineCRM.Application.DTOs;

/// <summary>Metadati di una comunicazione (circolare/PDF/Excel): non include il contenuto binario,
/// scaricato separatamente tramite l'endpoint di download.</summary>
public record ComunicazioneDto(
    int Id,
    string Titolo,
    string? Descrizione,
    string NomeFile,
    string TipoContenuto,
    long DimensioneByte,
    DateTime DataPubblicazione,
    string UtentePubblicazioneNomeCompleto,
    DestinatariComunicazione Destinatari
);

public record AggiornaDestinatariComunicazioneDto(DestinatariComunicazione Destinatari);
