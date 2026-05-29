using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dxs.Consigliere.Swagger;

/// <summary>
/// wave-A3 S6 — populates the OpenAPI <c>required</c> array of
/// each object schema from the source CLR type's nullable-
/// reference-type annotations. Non-nullable reference / value
/// properties (write state <c>NotNull</c>) are added to
/// <c>required</c>; nullable properties stay optional.
///
/// Why: Swashbuckle's default schema generator does NOT walk
/// NRT annotations — it only honours <c>[Required]</c>
/// attributes. Without this filter every C# DTO surfaced as
/// <c>property?: T</c> in the generated TS, so the admin UI
/// had to null-check fields that the backend never returns as
/// null. After S6, generated types lose <c>?:</c> on
/// non-nullable properties and `types/admin.ts` +
/// `types/auth.ts` can re-export the generated shapes
/// directly.
/// </summary>
public sealed class RequiredFromNrtFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema?.Properties is null || schema.Properties.Count == 0) return;
        if (context.Type is null) return;

        var required = new SortedSet<string>(schema.Required ?? new HashSet<string>(), StringComparer.Ordinal);
        var nrtContext = new NullabilityInfoContext();

        foreach (var (jsonName, _) in schema.Properties)
        {
            var clrMember = ResolveMember(context.Type, jsonName);
            if (clrMember is null) continue;
            try
            {
                var info = clrMember switch
                {
                    PropertyInfo p => nrtContext.Create(p),
                    FieldInfo f => nrtContext.Create(f),
                    _ => null,
                };
                if (info is null) continue;
                var notNull = info.WriteState == NullabilityState.NotNull
                    || info.ReadState == NullabilityState.NotNull;
                if (notNull)
                {
                    required.Add(jsonName);
                    // S6: also flip the schema-level `nullable`
                    // flag. openapi-typescript translates that
                    // into the `| null` union on the generated
                    // property; without this step every NotNull
                    // string still surfaces as `string | null`.
                    if (schema.Properties.TryGetValue(jsonName, out var openApiProperty)
                        && openApiProperty is not null)
                    {
                        openApiProperty.Nullable = false;
                    }
                }
                else if (schema.Properties.TryGetValue(jsonName, out var nullableProperty)
                    && nullableProperty is not null
                    && nullableProperty.Reference is not null)
                {
                    // wave-A4 S3: a nullable object-typed property is
                    // emitted by Swashbuckle as a bare `$ref` with no
                    // `nullable` flag — so openapi-typescript drops the
                    // `| null` and the generated property becomes
                    // `prop?: T` (T | undefined) even though the wire
                    // value is literally JSON `null` (e.g. a list
                    // response whose `summary` is null).
                    //
                    // OpenAPI 3.0 forbids sibling keywords next to a
                    // `$ref`, and Microsoft.OpenApi silently drops a
                    // `Nullable` set directly on the ref. Wrap the ref
                    // in an `allOf` + `nullable: true` so the union is
                    // preserved; openapi-typescript then emits
                    // `prop?: T | null` and the screens keep their
                    // null checks.
                    schema.Properties[jsonName] = new OpenApiSchema
                    {
                        Nullable = true,
                        AllOf = [new OpenApiSchema { Reference = nullableProperty.Reference }],
                    };
                }
            }
            catch
            {
                // NullabilityInfoContext can throw on exotic
                // generics; in that case we leave the property
                // optional rather than over-tightening the
                // contract.
            }
        }

        schema.Required = required;
    }

    private static MemberInfo ResolveMember(Type type, string jsonName)
    {
        // OpenAPI emits camelCase property names by default; walk
        // both public properties + fields, case-insensitive.
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        var prop = type.GetProperties(flags)
            .FirstOrDefault(p => string.Equals(p.Name, jsonName, StringComparison.OrdinalIgnoreCase));
        if (prop is not null) return prop;
        var field = type.GetFields(flags)
            .FirstOrDefault(f => string.Equals(f.Name, jsonName, StringComparison.OrdinalIgnoreCase));
        return field;
    }
}
