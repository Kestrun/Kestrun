---
name: kestrun-mcp
description: Build and maintain a Model Context Protocol (MCP) server for Kestrun. Use this skill for tasks involving MCP tool design, route inspection, OpenAPI exposure, runtime inspection, request validation, safe endpoint invocation, tests, and docs.
---

# Kestrun MCP skill

Use this skill when the task is about adding or improving MCP support in the Kestrun repository.

## Goal

Create and maintain a minimal, safe, extensible MCP server for Kestrun that exposes structured tools over existing Kestrun abstractions.

## Repository context

Kestrun is a hybrid PowerShell + ASP.NET Core framework built on Kestrel.

Important architectural expectations:
- Reuse existing Kestrun route registration, route metadata, runtime, and OpenAPI generation.
- Do not duplicate logic that already exists in the framework.
- Prefer thin adapters over new parallel models.
- Keep changes focused and idiomatic to the repo.

## First-choice MCP capabilities

For initial implementations, prioritize these tools:

1. `kestrun.list_routes`
   - Return all registered routes.
   - Include pattern, verbs, tags, summaries, request content types, response content types, and handler identity when available.

2. `kestrun.get_route`
   - Return detailed metadata for one route.
   - Include request/response schema details when available from OpenAPI metadata.

3. `kestrun.get_openapi`
   - Return generated OpenAPI output in structured JSON form.
   - Support selecting the target OpenAPI version if Kestrun supports multiple versions.

4. `kestrun.inspect_runtime`
   - Return safe runtime details such as uptime, start time, listeners, route counts, and environment information that is safe to expose.

5. `kestrun.validate_request`
   - Predict whether a proposed request matches route requirements.
   - Explain likely 400, 404, 406, or 415 outcomes.

6. `kestrun.invoke_route`
   - Only implement when explicitly requested or already enabled by configuration.
   - Must invoke through the normal Kestrun pipeline.
   - Must respect content-type validation, Accept negotiation, and existing framework behavior.

## Safety rules

- Default to local developer scenarios.
- Do not expose secrets, private keys, tokens, certificates, or hidden configuration values.
- Redact sensitive headers by default.
- Do not add destructive admin actions in v1.
- Gate request invocation behind explicit configuration if there is any risk of misuse.
- Do not create a broad remote execution surface.

## Design rules

- Prefer a dedicated project or module such as `src/Kestrun.Mcp` if that fits the repo structure.
- Keep transport/protocol handling separate from Kestrun business logic.
- Build small internal adapters or services if needed.
- Favor JSON-serializable outputs with stable shapes.
- Return structured errors for route-not-found, unsupported media type, unacceptable Accept header, binding failures, and internal exceptions.

## Implementation workflow

When asked to work on MCP support:

1. Inspect the repository for the best insertion point.
2. Identify the internal source of truth for:
   - routes
   - route options
   - OpenAPI metadata
   - runtime state
3. Propose the smallest viable implementation plan.
4. Implement incrementally.
5. Add or update tests.
6. Add or update docs.
7. Summarize what changed and how it was verified.

## Testing expectations

Always add tests for changed behavior.

Prioritize:
- route discovery
- route detail lookup
- OpenAPI retrieval
- missing route handling
- validation outcomes
- Accept mismatch behavior
- invalid content-type behavior
- request invocation if implemented

Prefer deterministic integration tests with small example Kestrun apps or scripts.

## Documentation expectations

When adding or changing MCP functionality:
- document the available MCP tools
- explain how to run the MCP server
- explain how to connect a client
- document the safety model
- include at least one simple example

## Kestrun-specific guidance

- Respect existing behavior for:
  - allowed request content types
  - default response content type
  - Accept negotiation
  - multipart and form handling
  - framework-generated 400/404/406/415 responses
- Prefer using existing metadata objects over parsing emitted text.
- Extend internal route models carefully if required fields are missing.

## Avoid

- broad refactors unrelated to MCP
- duplicated OpenAPI logic
- speculative abstractions with no immediate use
- placeholder code without tests and docs

## Definition of done

A task is complete when:
- the implementation fits the current Kestrun architecture
- tests cover the new behavior
- docs are updated
- the final summary explains what was added, what was verified, and any remaining follow-up work
