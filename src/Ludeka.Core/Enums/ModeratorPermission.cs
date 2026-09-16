using System;

namespace Ludeka.Core.Enums;

/// <summary>
/// Permisos granulares de moderación asignables a usuarios con rol Moderator.
/// Utiliza máscara de bits ([Flags]) para combinaciones eficientes.
/// </summary>
[Flags]
public enum ModeratorPermission
{
    /// <summary>
    /// Sin permisos de moderación adicionales.
    /// </summary>
    None = 0,

    /// <summary>
    /// Permiso para editar fichas técnicas y metadatos de juegos del catálogo.
    /// </summary>
    CanEditGames = 1 << 0, // 1

    /// <summary>
    /// Permiso para cargar y actualizar carátulas y fotografías de juegos en servidor.
    /// </summary>
    CanUploadImages = 1 << 1, // 2

    /// <summary>
    /// Permiso para dar de alta y editar fichas de editoriales y sus redes oficiales.
    /// </summary>
    CanManagePublishers = 1 << 2, // 4

    /// <summary>
    /// Permiso para dar de alta y editar fichas de creadores, autores e ilustradores.
    /// </summary>
    CanManageCreators = 1 << 3, // 8

    /// <summary>
    /// Permiso para aprobar o descartar vídeos en la bandeja de moderación multimedia (YouTube/Instagram).
    /// </summary>
    CanApproveMedia = 1 << 4, // 16

    /// <summary>
    /// Permiso para gestionar, clasificar y resolver reportes comunitarios de incidencias de catálogo.
    /// </summary>
    CanResolveReports = 1 << 5, // 32

    /// <summary>
    /// Permiso para dar de alta y editar tiendas y enlaces de compra afiliados.
    /// </summary>
    CanManageStoreLinks = 1 << 6, // 64

    /// <summary>
    /// Permiso para componer y publicar posts en la cuenta oficial de Instagram de Ludeka.
    /// </summary>
    CanPublishInstagram = 1 << 7, // 128

    /// <summary>
    /// Permiso para gestionar cuentas de usuario, roles y máscaras de permisos de moderación.
    /// </summary>
    CanManageUsers = 1 << 8, // 256

    /// <summary>
    /// Permiso para consultar la bitácora de auditoría editorial.
    /// </summary>
    CanViewAuditLog = 1 << 9, // 512

    /// <summary>
    /// Permiso para dar de alta, editar y eliminar grandes eventos lúdicos y sus carteles.
    /// </summary>
    CanManageEvents = 1 << 10, // 1024

    /// <summary>
    /// Permiso para gestionar los canales, webhooks y disparadores de las notificaciones comunitarias.
    /// </summary>
    CanManageNotifications = 1 << 11, // 2048

    /// <summary>
    /// Conjunción de todos los permisos granulares del sistema.
    /// </summary>
    All = CanEditGames | CanUploadImages | CanManagePublishers | CanManageCreators | CanApproveMedia | CanResolveReports | CanManageStoreLinks | CanPublishInstagram | CanManageUsers | CanViewAuditLog | CanManageEvents | CanManageNotifications // 4095
}
