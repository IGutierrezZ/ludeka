using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

public interface IInstagramApiClient
{
    Task<string> CreateMediaContainerAsync(string imageUrl, string caption, CancellationToken ct = default);
    Task<string> PublishMediaAsync(string creationId, CancellationToken ct = default);
    Task<string?> GetPermalinkAsync(string mediaId, CancellationToken ct = default);
    Task<IReadOnlyList<InstagramAccountMediaDto>> GetAccountReelsAsync(int limit = 25, CancellationToken ct = default);
}
