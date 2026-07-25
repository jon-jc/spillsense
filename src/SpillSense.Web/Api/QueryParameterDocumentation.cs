using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SpillSense.Domain.Geography;
using SpillSense.Domain.Incidents;

namespace SpillSense.Web.Api;

/// <summary>
/// Enriches the generated OpenAPI document for the shared incident query
/// parameters.
///
/// Those parameters bind as strings on purpose — it lets the query parser
/// collect every invalid value and name the accepted ones, instead of failing
/// with a framework binding error. The cost is that the generated document
/// would otherwise describe them as bare strings, so the accepted values and
/// their meaning are attached back here.
/// </summary>
public class QueryParameterDocumentationTransformer : IOpenApiOperationTransformer
{
    private static readonly Dictionary<string, (string Description, string[]? Values)> Documentation =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["county"] = ("Washington county name, without the \"County\" suffix (e.g. Thurston).", null),
            ["region"] = ("Department of Ecology regional office.", Enum.GetNames<EcologyRegion>()),
            ["medium"] = ("Environmental medium primarily affected.", Enum.GetNames<SpillMedium>()),
            ["category"] = ("Substance reporting category.", Enum.GetNames<SubstanceCategory>()),
            ["source"] = ("Kind of source the spill originated from.", Enum.GetNames<SourceType>()),
            ["status"] = ("Lifecycle state of the incident record.", Enum.GetNames<IncidentStatus>()),
            ["from"] = ("Inclusive lower bound on the report date (date or ISO 8601 date-time, UTC).", null),
            ["to"] = ("Exclusive upper bound on the report date.", null),
            ["search"] = ("Case-insensitive text match across substance, description, responsible party, location, waterbody, and report number.", null),
            ["bbox"] = ("Spatial filter as minLon,minLat,maxLon,maxLat in WGS 84.", null),
            ["minGallons"] = ("Only incidents with an estimated quantity at or above this value.", null),
            ["hasCoordinates"] = ("Restrict to incidents that do (true) or do not (false) carry map coordinates.", null),
            ["page"] = ("1-based page number. Defaults to 1.", null),
            ["pageSize"] = ("Records per page, between 1 and 200. Defaults to 25.", null),
            ["sort"] = ("Sort order; prefix with '-' for descending. Defaults to -reportedAt.",
                ["reportedAt", "-reportedAt", "quantity", "-quantity"]),
            ["year"] = ("Calendar year to report on, between 1990 and 2100.", null),
            ["reportNumber"] = ("Report number natural key, e.g. ERTS-2025-000123.", null),
            ["id"] = ("Import run identifier.", null),
        };

    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (var parameter in operation.Parameters ?? [])
        {
            if (parameter is not OpenApiParameter concrete || concrete.Name is null)
            {
                continue;
            }

            if (!Documentation.TryGetValue(concrete.Name, out var doc))
            {
                continue;
            }

            concrete.Description ??= doc.Description;

            if (doc.Values is not null && concrete.Schema is OpenApiSchema schema)
            {
                schema.Enum = [.. doc.Values.Select(v => (JsonNode)JsonValue.Create(v)!)];
            }
        }

        return Task.CompletedTask;
    }
}
