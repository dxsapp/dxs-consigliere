#nullable enable

using Dxs.Consigliere.Swagger;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dxs.Consigliere.Tests.Swagger;

/// <summary>
/// wave-A3 S6 — pins the NRT-aware required-field inference.
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
    public void Adds_NotNull_reference_property_to_required()
    {
        var schema = BuildSchema(["notNullRef", "nullableRef"]);
        new RequiredFromNrtFilter().Apply(schema, Ctx(typeof(Mixed)));

        Assert.Contains("notNullRef", schema.Required);
        Assert.DoesNotContain("nullableRef", schema.Required);
    }

    [Fact]
    public void Adds_value_type_property_to_required()
    {
        var schema = BuildSchema(["notNullValue", "nullableValue"]);
        new RequiredFromNrtFilter().Apply(schema, Ctx(typeof(Mixed)));

        Assert.Contains("notNullValue", schema.Required);
        Assert.DoesNotContain("nullableValue", schema.Required);
    }

    [Fact]
    public void Preserves_existing_required_entries()
    {
        var schema = BuildSchema(["notNullRef", "nullableRef"]);
        schema.Required.Add("nullableRef");
        new RequiredFromNrtFilter().Apply(schema, Ctx(typeof(Mixed)));

        Assert.Contains("nullableRef", schema.Required);
        Assert.Contains("notNullRef", schema.Required);
    }

    [Fact]
    public void Ignores_properties_with_no_clr_match()
    {
        var schema = BuildSchema(["doesNotExist"]);
        new RequiredFromNrtFilter().Apply(schema, Ctx(typeof(Mixed)));
        Assert.Empty(schema.Required);
    }

    [Fact]
    public void Noop_when_schema_has_no_properties()
    {
        var schema = new OpenApiSchema { Type = "object" };
        new RequiredFromNrtFilter().Apply(schema, Ctx(typeof(Mixed)));
        Assert.Empty(schema.Required);
    }

    private static OpenApiSchema BuildSchema(string[] propertyNames)
    {
        var schema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>(),
            Required = new HashSet<string>(),
        };
        foreach (var p in propertyNames)
            schema.Properties[p] = new OpenApiSchema { Type = "string" };
        return schema;
    }

    private static SchemaFilterContext Ctx(Type type) =>
        new(type, schemaGenerator: null!, schemaRepository: new SchemaRepository());
}
