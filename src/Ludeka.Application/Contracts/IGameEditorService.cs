using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

public interface IGameEditorService
{
    Task<GameDetailDto> UpdateGameAsync(
        UpdateGameDetailsCommand command,
        CancellationToken ct = default);

    Task<IReadOnlyList<GameEditLogDto>> GetEditLogsAsync(
        Guid gameId,
        CancellationToken ct = default);

    Task<GameDetailDto> AssociateBggIdAsync(
        Guid gameId,
        int bggId,
        CancellationToken ct = default);

    Task<GameBggSyncResultDto> ForceSyncFromBggAsync(
        Guid gameId,
        CancellationToken ct = default);
}

