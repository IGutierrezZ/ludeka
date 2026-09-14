using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Features.Bgg;

/// <summary>
/// Parser de streaming de alto rendimiento para el volcado masivo de clasificación de BGG (bg_ranks en formato CSV o GZ).
/// Aplica el filtrado de tracción comunitaria (usersrated >= minUsersRated).
/// </summary>
public static class BggDumpParser
{
    public static async IAsyncEnumerable<BggRanksDumpRowDto> ParseRanksDumpAsync(
        Stream inputStream,
        int minUsersRated = 30,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputStream);

        Stream workingStream = inputStream;
        bool isGzip = false;

        // Comprobación de cabecera GZip (Magic Numbers 0x1F, 0x8B)
        if (inputStream.CanSeek && inputStream.Length >= 2)
        {
            int b1 = inputStream.ReadByte();
            int b2 = inputStream.ReadByte();
            inputStream.Position = 0;

            if (b1 == 0x1F && b2 == 0x8B)
            {
                isGzip = true;
            }
        }

        if (isGzip)
        {
            workingStream = new GZipStream(inputStream, CompressionMode.Decompress, leaveOpen: true);
        }

        using var reader = new StreamReader(workingStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        string? headerLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(headerLine)) yield break;

        var headers = ParseCsvLine(headerLine);
        var columnMap = BuildColumnMap(headers);

        if (!columnMap.ContainsKey("id") || !columnMap.ContainsKey("name"))
        {
            throw new FormatException("El archivo CSV no contiene las columnas mínimas requeridas ('id' y 'name').");
        }

        int idIdx = columnMap["id"];
        int nameIdx = columnMap["name"];
        int yearIdx = columnMap.GetValueOrDefault("year", -1);
        int rankIdx = columnMap.GetValueOrDefault("rank", -1);
        int usersRatedIdx = columnMap.GetValueOrDefault("usersrated", -1);
        int bayesIdx = columnMap.GetValueOrDefault("bayes", -1);
        int avgIdx = columnMap.GetValueOrDefault("average", -1);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (ct.IsCancellationRequested) yield break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseCsvLine(line);
            if (fields.Count <= Math.Max(idIdx, nameIdx)) continue;

            if (!int.TryParse(fields[idIdx], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bggId) || bggId <= 0)
            {
                continue;
            }

            string title = fields[nameIdx].Trim();
            if (string.IsNullOrWhiteSpace(title)) continue;

            int usersRated = 0;
            if (usersRatedIdx >= 0 && usersRatedIdx < fields.Count)
            {
                int.TryParse(fields[usersRatedIdx], NumberStyles.Integer, CultureInfo.InvariantCulture, out usersRated);
            }

            // Filtrado de relevancia: sólo juegos con suficientes votos
            if (usersRated < minUsersRated) continue;

            int? year = null;
            if (yearIdx >= 0 && yearIdx < fields.Count && int.TryParse(fields[yearIdx], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedYear) && parsedYear > 0)
            {
                year = parsedYear;
            }

            int? rank = null;
            if (rankIdx >= 0 && rankIdx < fields.Count && int.TryParse(fields[rankIdx], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedRank) && parsedRank > 0)
            {
                rank = parsedRank;
            }

            double bayesAverage = 0.0;
            if (bayesIdx >= 0 && bayesIdx < fields.Count)
            {
                double.TryParse(fields[bayesIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out bayesAverage);
            }

            double averageRating = 0.0;
            if (avgIdx >= 0 && avgIdx < fields.Count)
            {
                double.TryParse(fields[avgIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out averageRating);
            }

            yield return new BggRanksDumpRowDto(
                BggId: bggId,
                Title: title,
                YearPublished: year,
                BggRank: rank,
                BayesAverage: bayesAverage,
                AverageRating: averageRating,
                UsersRated: usersRated
            );
        }
    }

    private static Dictionary<string, int> BuildColumnMap(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Count; i++)
        {
            string raw = headers[i].Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");

            if (raw == "id" || raw == "bggid" || raw == "gameid")
                map["id"] = i;
            else if (raw == "name" || raw == "title" || raw == "gamename")
                map["name"] = i;
            else if (raw == "yearpublished" || raw == "year" || raw == "publicationyear")
                map["year"] = i;
            else if (raw == "rank" || raw == "bggrank" || raw == "boardgamerank")
                map["rank"] = i;
            else if (raw == "usersrated" || raw == "numvotes" || raw == "votes" || raw == "ratings")
                map["usersrated"] = i;
            else if (raw == "bayesaverage" || raw == "bayes" || raw == "geekrating")
                map["bayes"] = i;
            else if (raw == "average" || raw == "averagerating" || raw == "rating")
                map["average"] = i;
        }

        return map;
    }

    public static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(line)) return result;

        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++; // Skip escape quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString().Trim());
        return result;
    }
}
