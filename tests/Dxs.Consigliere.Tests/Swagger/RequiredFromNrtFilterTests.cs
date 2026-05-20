#nullable enable

using Dxs.Consigliere.Swagger;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dxs.Consigliere.Tests.Swagger;

/// <summary>
/// wave-A3 S6 — pins both halves of the NRT-aware schema
/// filter: `required` membership AND the `Nullable = false`
/// flag (openapi-typescript drops `| null` from the generated
/// TS only when both are set). S6-audit L1 fix: the original
/// suite only pinned the `required` half, so a future edit
/// that dropped the `Nullable = false` assignment would have
/// silently regressed the contract-tightening slice.
/// </summary>
public sealed class RequiredFromNrtFilterTests
{
    private sealed class Mixed
    {
        public string NotNullRef { get; set; } = string.Empty;
        public string? NullableRef { get; set; }
        public int NotNullValue { get; set; }
        public int? NullableValue { get; set; }
    }

    [Fact]
    public void Adds_NotNull_reference_property_to_required_AND_flips_nullable_false()
    {
        // S6-audit L1: seed every property as `Nullable = true`
        // so the assertion proves the filter actively flipped
        // the flag (rather than accidentally inheriting a
        // pre-existing `false` default).
        var schema = BuildSchema(["notNullRef", "nullableRef"], nullableSeed: true);
        new RequiredFromNrtFilter().Apply(schema, Ctx(typeof(Mixed)));

        Assert.Contains("notNullRef", schema.Required);
        Assert.DoesNotContain("nullableRef", schema.Required);

        Assert.False(schema.Properties["notNullRef"].Nullable);
        // Nullable references stay nullable — the filter must
        // NOT over-tighten.
        Assert.True(schema.Properties["nullableRef"].Nullable);
    }

    [Fact]
    public void Adds_value_type_property_to_required_AND_flips_nullable_false()
    {
        var schema = BuildSchema(["notNullValue", "nullableValue"], nullableSeed: true);
        new RequiredFromNrtFilter().Apply(schema, Ctx(typeof(Mixed)));

        Assert.Contains("notNullValue", schema.Required);
        Assert.DoesNotContain("nullableValue", schema.Required);

        Assert.False(schema.Properties["notNullValue"].Nullable);
        Assert.True(schema.Properties["nullableValue"].Nullable);
    }

    [Fact]
    public void Preserves_existing_required_entries_without_forcing_nullable_to_false_for_nullable_members()
    {
        // A nullable CLR member explicitly pre-marked
        // `required` (e.g. via `[Required]` on a nullable
        // property) MUST stay in `required` but MUST NOT
        // collapse to `Nullable = false` — the property is
        // intrinsically nullable.
        var schema = BuildSchema(["notNullRef", "nullableRef"], nullableSeed: true);
        schema.Required.Add("nullableRef");
        new RequiredFromNrtFilter().Apply(schema, Ctx(typeof(Mixed)));

        Assert.Contains("nullableRef", schema.Required);
        Assert.Contains("notNullRef", schema.Required);

        Assert.False(schema.Properties["notNullRef"].Nullable);
        // The nullable ref's `Nullable: true` must survive.
        Assert.True(schema.Properties["nullableRef"].Nullable);
    }

    [Fact]
    public void Ignores_properties_with_no_clr_match()
    {
        var schema = BuildSchema(["doesNotExist"], nullableSeed: true);
        new RequiredFromNrtFilter().Apply(schema, Ctx(typeof(Mixed)));
        Assert.Empty(schema.Required);
        // The unrecognised property is left untouched on every
        // axis — the filter doesn't randomly tighten things it
        // can't reason about.
        Assert.True(schema.Properties["doesNotExist"].Nullable);
    }

    [Fact]
    public void Noop_when_schema_has_no_properties()
    {
        var schema = new OpenApiSchema { Type = "object" };
        new RequiredFromNrtFilter().Apply(schema, Ctx(typeof(Mixed)));
        Assert.Empty(schema.Required);
    }

    private static OpenApiSchema BuildSchema(string[] propertyNames, bool nullableSeed)
    {
        var schema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>(),
            Required = new HashSet<string>(),
        };
        foreach (var p in propertyNames)
            schema.Properties[p] = new OpenApiSchema { Type = "string", Nullable = nullableSeed };
        return schema;
    }

    private static SchemaFilterContext Ctx(Type type) =>
        new(type, schemaGenerator: null!, schemaRepository: new SchemaRepository());
}
