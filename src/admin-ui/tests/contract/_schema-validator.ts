import Ajv2020, { type ValidateFunction } from "ajv/dist/2020";
import addFormats from "ajv-formats";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

/**
 * wave-A2 S2 — runtime shape validator that reads the committed
 * OpenAPI document and produces an ajv validator per
 * `components.schemas.<Name>`. Each contract test calls
 * `expectShape(name, body)` to assert the response wire shape
 * matches the contract Swashbuckle emitted.
 *
 * We deliberately do NOT validate against the path-level
 * response schema; we walk straight to the schema name (e.g.
 * `P2pHealthDto`). This keeps the assertion focused on the
 * structural DTO and skips the wrapping `application/json` +
 * `200`/`400`/etc. branches.
 */

const HERE = resolve(import.meta.dirname);
const SWAGGER_PATH = resolve(HERE, "..", "..", "contracts", "swagger.json");

interface OpenApiDocument {
  components?: {
    schemas?: Record<string, unknown>;
  };
}

const document = JSON.parse(readFileSync(SWAGGER_PATH, "utf-8")) as OpenApiDocument;
const schemas = document.components?.schemas ?? {};

const ajv = new Ajv2020({
  strict: false, // OpenAPI uses keywords like `nullable`, `example`, etc.
  allErrors: true,
});
addFormats(ajv);

// OpenAPI uses `$ref: "#/components/schemas/X"` everywhere; Ajv2020
// rejects `#` inside `$id`. Pre-walk every schema and rewrite refs
// to bare names so we can register each schema with `$id: name`.
const rewrittenSchemas: Record<string, Record<string, unknown>> = {};
for (const [name, schema] of Object.entries(schemas)) {
  if (typeof schema !== "object" || schema === null) continue;
  rewrittenSchemas[name] = rewriteRefs(schema) as Record<string, unknown>;
}
for (const [name, schema] of Object.entries(rewrittenSchemas)) {
  ajv.addSchema({ ...schema, $id: name });
}

function rewriteRefs(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(rewriteRefs);
  if (value && typeof value === "object") {
    const out: Record<string, unknown> = {};
    for (const [k, child] of Object.entries(value as Record<string, unknown>)) {
      if (k === "$ref" && typeof child === "string") {
        out[k] = child.startsWith("#/components/schemas/")
          ? child.slice("#/components/schemas/".length)
          : child;
      } else {
        out[k] = rewriteRefs(child);
      }
    }
    return out;
  }
  return value;
}

const compiled = new Map<string, ValidateFunction>();

function getValidator(name: string): ValidateFunction {
  const cached = compiled.get(name);
  if (cached) return cached;
  const schema = rewrittenSchemas[name];
  if (!schema) {
    throw new Error(`[schema-validator] no schema named ${name} in swagger.json`);
  }
  // `getSchema` returns the function for an already-registered
  // schema; fall back to `compile` for safety.
  const fn = ajv.getSchema(name) ?? ajv.compile(schema);
  compiled.set(name, fn);
  return fn;
}

export function expectShape(name: string, body: unknown): void {
  const validate = getValidator(name);
  const ok = validate(body);
  if (!ok) {
    const errors = (validate.errors ?? [])
      .map((e) => `  ${e.instancePath || "(root)"} ${e.message}`)
      .join("\n");
    throw new Error(`[shape ${name}] validation failed:\n${errors}\nbody: ${JSON.stringify(body).slice(0, 800)}`);
  }
}

/** Tiny accessor used by tests to assert a schema exists. */
export function schemaNames(): string[] {
  return Object.keys(schemas);
}
