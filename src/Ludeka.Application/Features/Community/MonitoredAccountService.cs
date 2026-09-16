using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;

namespace Ludeka.Application.Features.Community;

public class MonitoredAccountService : IMonitoredAccountService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanApproveMedia' para gestionar las cuentas monitorizadas.";

    private readonly IMonitoredAccountRepository _repository;
    private readonly IPublisherRepository _publisherRepository;
    private readonly ICreatorRepository _creatorRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly ILogger<MonitoredAccountService> _logger;
    private readonly ISessionPermissionGuard? _permissionGuard;

    public MonitoredAccountService(
        IMonitoredAccountRepository repository,
        IPublisherRepository publisherRepository,
        ICreatorRepository creatorRepository,
        IStoreRepository storeRepository,
        ILogger<MonitoredAccountService> logger,
        ISessionPermissionGuard? permissionGuard = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _publisherRepository = publisherRepository ?? throw new ArgumentNullException(nameof(publisherRepository));
        _creatorRepository = creatorRepository ?? throw new ArgumentNullException(nameof(creatorRepository));
        _storeRepository = storeRepository ?? throw new ArgumentNullException(nameof(storeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _permissionGuard = permissionGuard;
    }

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1): el panel de canales
    /// monitorizados escribe cuentas y su sincronización con el directorio.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanApproveMedia, DenialMessage, ct);

    public async Task<IReadOnlyList<MonitoredAccountDto>> GetAccountsAsync(
        SocialPlatform? platform = null,
        MonitoredAccountType? type = null,
        bool? onlyEnabled = null,
        CancellationToken ct = default)
    {
        var accounts = await _repository.GetAllAsync(platform, type, onlyEnabled, ct);
        var result = new List<MonitoredAccountDto>(accounts.Count);
        foreach (var a in accounts)
        {
            result.Add(MonitoredAccountDto.FromEntity(a));
        }
        return result;
    }

    public async Task<MonitoredAccountDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var account = await _repository.GetByIdAsync(id, ct);
        return account != null ? MonitoredAccountDto.FromEntity(account) : null;
    }

    public async Task<MonitoredAccountDto> CreateAccountAsync(MonitoredAccountDto dto, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("El nombre de la cuenta no puede estar vacío.", nameof(dto.Name));

        if (string.IsNullOrWhiteSpace(dto.HandleOrChannelId))
            throw new ArgumentException("El handle o identificador no puede estar vacío.", nameof(dto.HandleOrChannelId));

        if (string.IsNullOrWhiteSpace(dto.ProfileUrl))
            throw new ArgumentException("La URL del perfil no puede estar vacía.", nameof(dto.ProfileUrl));

        if (await _repository.ExistsAsync(dto.Platform, dto.HandleOrChannelId, ct))
            throw new InvalidOperationException($"Ya existe una cuenta monitorizada para {dto.Platform} con el identificador '{dto.HandleOrChannelId}'.");

        var account = new MonitoredSocialAccount(
            name: dto.Name,
            platform: dto.Platform,
            handleOrChannelId: dto.HandleOrChannelId,
            accountType: dto.AccountType,
            profileUrl: dto.ProfileUrl,
            notes: dto.Notes,
            isEnabled: dto.IsEnabled);

        var saved = await _repository.AddAsync(account, ct);
        _logger.LogInformation("Cuenta monitorizada creada: {Name} ({Platform})", saved.Name, saved.Platform);

        return MonitoredAccountDto.FromEntity(saved);
    }

    public async Task UpdateAccountAsync(MonitoredAccountDto dto, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        ArgumentNullException.ThrowIfNull(dto);

        var account = await _repository.GetByIdAsync(dto.Id, ct)
            ?? throw new KeyNotFoundException($"No se encontró ninguna cuenta monitorizada con ID '{dto.Id}'.");

        account.UpdateDetails(
            name: dto.Name,
            platform: dto.Platform,
            handleOrChannelId: dto.HandleOrChannelId,
            accountType: dto.AccountType,
            profileUrl: dto.ProfileUrl,
            notes: dto.Notes);

        await _repository.UpdateAsync(account, ct);
        _logger.LogInformation("Cuenta monitorizada {Id} actualizada", account.Id);
    }

    public async Task ToggleAccountStatusAsync(Guid id, bool isEnabled, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var account = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"No se encontró ninguna cuenta monitorizada con ID '{id}'.");

        account.ToggleStatus(isEnabled);
        await _repository.UpdateAsync(account, ct);
        _logger.LogInformation("Estado de cuenta monitorizada {Id} cambiado a: {Status}", id, isEnabled);
    }

    public async Task DeleteAccountAsync(Guid id, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        await _repository.DeleteAsync(id, ct);
        _logger.LogInformation("Cuenta monitorizada {Id} eliminada", id);
    }

    public async Task<int> SyncFromDirectoryAsync(CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        _logger.LogInformation("Iniciando sincronización de cuentas desde el directorio de editoriales, creadores y tiendas");
        var syncedCount = 0;

        // 1. Editoriales
        var publishers = await _publisherRepository.GetAllAsync(ct);
        foreach (var pub in publishers)
        {
            foreach (var link in pub.SocialLinks)
            {
                if (!IsRelevantPlatform(link.Platform))
                    continue;

                var handle = !string.IsNullOrWhiteSpace(link.Handle) ? link.Handle : pub.Name;
                if (!await _repository.ExistsAsync(link.Platform, handle, ct))
                {
                    var account = new MonitoredSocialAccount(
                        name: pub.Name,
                        platform: link.Platform,
                        handleOrChannelId: handle,
                        accountType: MonitoredAccountType.Publisher,
                        profileUrl: link.Url,
                        notes: $"Importado desde Editorial {pub.Name}");

                    await _repository.AddAsync(account, ct);
                    syncedCount++;
                }
            }
        }

        // 2. Creadores / Divulgadores
        var creators = await _creatorRepository.GetAllAsync(ct);
        foreach (var creator in creators)
        {
            foreach (var link in creator.SocialLinks)
            {
                if (!IsRelevantPlatform(link.Platform))
                    continue;

                var handle = !string.IsNullOrWhiteSpace(link.Handle) ? link.Handle : creator.Name;
                if (!await _repository.ExistsAsync(link.Platform, handle, ct))
                {
                    var account = new MonitoredSocialAccount(
                        name: creator.Name,
                        platform: link.Platform,
                        handleOrChannelId: handle,
                        accountType: MonitoredAccountType.Creator,
                        profileUrl: link.Url,
                        notes: $"Importado desde Creador {creator.Name}");

                    await _repository.AddAsync(account, ct);
                    syncedCount++;
                }
            }
        }

        // 3. Tiendas
        var stores = await _storeRepository.GetAllAsync(ct);
        foreach (var store in stores)
        {
            foreach (var link in store.SocialLinks)
            {
                if (!IsRelevantPlatform(link.Platform))
                    continue;

                var handle = !string.IsNullOrWhiteSpace(link.Handle) ? link.Handle : store.Name;
                if (!await _repository.ExistsAsync(link.Platform, handle, ct))
                {
                    var account = new MonitoredSocialAccount(
                        name: store.Name,
                        platform: link.Platform,
                        handleOrChannelId: handle,
                        accountType: MonitoredAccountType.Store,
                        profileUrl: link.Url,
                        notes: $"Importado desde Tienda {store.Name}");

                    await _repository.AddAsync(account, ct);
                    syncedCount++;
                }
            }
        }

        _logger.LogInformation("Sincronización completada. Se añadieron {Count} nuevas cuentas monitorizadas.", syncedCount);
        return syncedCount;
    }

    private static bool IsRelevantPlatform(SocialPlatform platform)
    {
        return platform is SocialPlatform.Instagram or SocialPlatform.YouTube or SocialPlatform.Website or SocialPlatform.TikTok;
    }
}
