namespace Ludeka.Application.DTOs;

/// <summary>
/// DTO con el resultado de la deducción realizada por el asistente de IA para una novedad editorial.
/// </summary>
public record AiReleaseMatchResultDto(
    int? SuggestedBggId,
    string? SuggestedTitle,
    string? Reasoning,
    string? CandidateCoverUrl = null,
    int? CandidateYearPublished = null
);
