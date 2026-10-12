using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Features.Seo;

/// <summary>
/// Generador de metadatos SEO y datos estructurados Schema.org (JSON-LD) para fichas de juego.
/// </summary>
public static class GameSeoHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    /// <summary>
    /// Construye una meta descripción optimizada para motores de búsqueda (máx ~160-165 caracteres).
    /// </summary>
    public static string BuildMetaDescription(GameDetailDto game)
    {
        if (game == null) return string.Empty;

        var title = !string.IsNullOrWhiteSpace(game.SpanishTitle) ? game.SpanishTitle : game.OriginalTitle;
        var year = game.YearPublished > 0 ? $" ({game.YearPublished})" : string.Empty;

        var duration = game.Duration != null && game.Duration.MinMinutes > 0
            ? (game.Duration.MinMinutes == game.Duration.MaxMinutes
                ? $"{game.Duration.MinMinutes} min"
                : $"{game.Duration.MinMinutes}–{game.Duration.MaxMinutes} min")
            : "duración variable";

        var players = GetPlayerCountText(game);

        var publisher = !string.IsNullOrWhiteSpace(game.SpanishPublisher)
            ? game.SpanishPublisher
            : (!string.IsNullOrWhiteSpace(game.Publisher) ? game.Publisher : "Editorial de juegos");

        var designer = !string.IsNullOrWhiteSpace(game.Designer)
            ? game.Designer
            : null;

        string description;
        if (!string.IsNullOrWhiteSpace(designer))
        {
            description = $"Ficha de {title}{year}: partidas de {duration} para {players}. Diseñado por {designer} y publicado por {publisher}. Opiniones y notas en Ludeka.";
        }
        else
        {
            description = $"Ficha de {title}{year}: partidas de {duration} para {players}, editado por {publisher}. Opiniones, notas y escalabilidad en Ludeka.";
        }

        if (description.Length <= 165)
        {
            return description;
        }

        // Si excede 165 caracteres, recortar de forma limpia en el último espacio
        var truncated = description[..162];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > 120)
        {
            truncated = truncated[..lastSpace];
        }
        return truncated + "...";
    }

    /// <summary>
    /// Construye el bloque JSON-LD con Schema.org BoardGame y FAQPage.
    /// </summary>
    public static string BuildStructuredDataJson(GameDetailDto game, string canonicalUrl)
    {
        if (game == null) return "{}";

        var title = !string.IsNullOrWhiteSpace(game.SpanishTitle) ? game.SpanishTitle : game.OriginalTitle;
        var publisher = !string.IsNullOrWhiteSpace(game.SpanishPublisher) ? game.SpanishPublisher : game.Publisher;
        var minAge = game.Age != null && game.Age.BoxAge > 0
            ? game.Age.BoxAge
            : (game.Age?.CommunityAge > 0 ? game.Age.CommunityAge : 10);

        var (minPlayers, maxPlayers) = GetPlayerMinMax(game);
        var durationMinutes = game.Duration != null && game.Duration.MaxMinutes > 0
            ? game.Duration.MaxMinutes
            : (game.Duration?.MinMinutes > 0 ? game.Duration.MinMinutes : 60);

        // 1. Entidad BoardGame
        var boardGameNode = new JsonObject
        {
            ["@type"] = "BoardGame",
            ["name"] = title,
            ["url"] = canonicalUrl
        };

        if (!string.IsNullOrWhiteSpace(game.OriginalTitle) && !string.Equals(game.OriginalTitle, title, StringComparison.OrdinalIgnoreCase))
        {
            boardGameNode["alternateName"] = game.OriginalTitle;
        }

        if (!string.IsNullOrWhiteSpace(game.CoverImageUrl))
        {
            boardGameNode["image"] = game.CoverImageUrl;
        }

        if (!string.IsNullOrWhiteSpace(game.Description))
        {
            boardGameNode["description"] = CleanDescriptionText(game.Description);
        }

        boardGameNode["numberOfPlayers"] = new JsonObject
        {
            ["@type"] = "QuantitativeValue",
            ["minValue"] = minPlayers,
            ["maxValue"] = maxPlayers
        };

        boardGameNode["suggestedMinAge"] = minAge;
        boardGameNode["timeRequired"] = $"PT{durationMinutes}M";

        if (!string.IsNullOrWhiteSpace(game.Designer))
        {
            var authorsArray = new JsonArray();
            var designers = game.Designer.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var d in designers)
            {
                authorsArray.Add(new JsonObject
                {
                    ["@type"] = "Person",
                    ["name"] = d
                });
            }
            boardGameNode["author"] = authorsArray;
        }

        if (!string.IsNullOrWhiteSpace(publisher))
        {
            boardGameNode["publisher"] = new JsonObject
            {
                ["@type"] = "Organization",
                ["name"] = publisher
            };
        }

        var rating = game.LudistRating > 0 ? game.LudistRating : game.BggRating;
        if (rating > 0)
        {
            boardGameNode["aggregateRating"] = new JsonObject
            {
                ["@type"] = "AggregateRating",
                ["ratingValue"] = Math.Round(rating, 1).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                ["bestRating"] = "10",
                ["worstRating"] = "1",
                ["ratingCount"] = 100
            };
        }

        // 2. Entidad FAQPage (Respuestas directas a consultas típicas de Google)
        var faqQuestions = new JsonArray();

        // Pregunta 1: Jugadores
        var playersAnswer = minPlayers == maxPlayers
            ? $"{title} está diseñado específicamente para {minPlayers} personas."
            : $"{title} se puede jugar de {minPlayers} a {maxPlayers} jugadores." +
              (!string.IsNullOrWhiteSpace(game.IdealPlayerCountText) ? $" El número ideal recomendado por la comunidad es a {game.IdealPlayerCountText}." : string.Empty);

        faqQuestions.Add(new JsonObject
        {
            ["@type"] = "Question",
            ["name"] = $"¿Cuántos jugadores pueden jugar a {title}?",
            ["acceptedAnswer"] = new JsonObject
            {
                ["@type"] = "Answer",
                ["text"] = playersAnswer
            }
        });

        // Pregunta 2: Duración
        var durationText = game.Duration != null && game.Duration.MinMinutes > 0
            ? (game.Duration.MinMinutes == game.Duration.MaxMinutes
                ? $"unos {game.Duration.MinMinutes} minutos"
                : $"entre {game.Duration.MinMinutes} y {game.Duration.MaxMinutes} minutos")
            : "aproximadamente 45-60 minutos";

        faqQuestions.Add(new JsonObject
        {
            ["@type"] = "Question",
            ["name"] = $"¿Cuánto dura una partida de {title}?",
            ["acceptedAnswer"] = new JsonObject
            {
                ["@type"] = "Answer",
                ["text"] = $"Una partida de {title} suele durar {durationText}, dependiendo del número de jugadores y su experiencia en la mesa."
            }
        });

        // Pregunta 3: Edad recomendada
        faqQuestions.Add(new JsonObject
        {
            ["@type"] = "Question",
            ["name"] = $"¿A partir de qué edad se recomienda jugar a {title}?",
            ["acceptedAnswer"] = new JsonObject
            {
                ["@type"] = "Answer",
                ["text"] = $"{title} está recomendado para jugadores a partir de los {minAge} años."
            }
        });

        // Pregunta 4: Autor y editorial
        if (!string.IsNullOrWhiteSpace(game.Designer) || !string.IsNullOrWhiteSpace(publisher))
        {
            var authorText = !string.IsNullOrWhiteSpace(game.Designer) ? $"diseñado por {game.Designer}" : string.Empty;
            var pubText = !string.IsNullOrWhiteSpace(publisher) ? $"editado por {publisher}" : string.Empty;
            var creditsText = !string.IsNullOrWhiteSpace(authorText) && !string.IsNullOrWhiteSpace(pubText)
                ? $"{authorText} y {pubText}"
                : (!string.IsNullOrWhiteSpace(authorText) ? authorText : pubText);

            faqQuestions.Add(new JsonObject
            {
                ["@type"] = "Question",
                ["name"] = $"¿Quién diseñó {title} y qué editorial lo publica?",
                ["acceptedAnswer"] = new JsonObject
                {
                    ["@type"] = "Answer",
                    ["text"] = $"{title} es un juego {creditsText}."
                }
            });
        }

        var faqNode = new JsonObject
        {
            ["@type"] = "FAQPage",
            ["mainEntity"] = faqQuestions
        };

        var root = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new JsonArray { boardGameNode, faqNode }
        };

        return root.ToJsonString(JsonOptions);
    }

    private static string GetPlayerCountText(GameDetailDto game)
    {
        var (min, max) = GetPlayerMinMax(game);
        return min == max ? $"{min} jugadores" : $"{min}–{max} jugadores";
    }

    private static (int Min, int Max) GetPlayerMinMax(GameDetailDto game)
    {
        if (game.Scalability != null && game.Scalability.Count > 0)
        {
            var min = game.Scalability.Min(s => s.PlayerCount);
            var max = game.Scalability.Max(s => s.PlayerCount);
            if (min > 0 && max > 0)
            {
                return (min, max);
            }
        }

        return (1, 4);
    }

    private static string CleanDescriptionText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var text = System.Text.RegularExpressions.Regex.Replace(raw, "<.*?>", string.Empty);
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length > 250 ? text[..247] + "..." : text;
    }
}
