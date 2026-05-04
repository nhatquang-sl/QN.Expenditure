---
description: Write a technical feature document from a requirement, then self-review and improve it as a senior software engineer. Outputs a polished .md file ready for implementation.
argument-hint: <requirement description or paste requirement text>
model: claude-opus-4-6
---

ultrathink

You are acting as a senior software engineer on this project. Your task is to produce a polished technical feature document from the requirement provided below.

**Requirement:**
$ARGUMENTS

---

## Phase 1 — Author

Read the codebase as needed to understand existing patterns, entities, conventions, and related features before writing. Then produce a first-draft technical document using the **Document Template** below as your structure guide.

### Document Template

Use the sections that apply to the feature. Omit sections marked "(if applicable)" when not relevant. Keep the order exactly as listed.

---

```
# <Feature Name>

## Overview
2–4 sentences: what it does, when/how it is triggered, why it exists, what problem it solves.
**Module Location**: `path/to/module/`
**Scope**: e.g. "v1: BTCUSDT only" or "user-scoped"

---

## Data Model

### Entity: <EntityName> (New | Existing)

Column table (use for new or modified columns):

| Column | Type | Nullable | Default | Constraints | Notes |
|---|---|---|---|---|---|
| Id | int | No | identity | PK | |
| ... | | | | | |

C# entity definition (code block):
- All DateTime / DateTime? properties must use HasPrecision(0) in EF Core config → datetime2(0)
- decimal columns need HasPrecision(total, scale)
- string columns need HasMaxLength

### DTOs (if applicable)
Record definitions for input/output data transfer objects.

### Business Rules
Numbered list of domain rules (validation, computation, state transitions).

---

## Algorithm (for background jobs, commands with non-trivial logic)

Numbered pseudocode steps. For every non-obvious decision add a callout:
> **Why ...?**
> Explanation.

---

## Data Access

Representative C# snippets for the most important DB interactions.
Follow project patterns:
- Primary constructors for dependency injection
- File-scoped namespaces
- async/await with CancellationToken throughout
- CQRS with MediatR (Commands for writes, Queries for reads)
- Record types for Commands/Queries/DTOs

---

## Backend Architecture

### Domain Layer
New/modified files in Cex.Domain (entities, enums, value objects).

### Infrastructure Layer
New/modified files in Cex.Infrastructure:
- EF Core entity configuration (HasPrecision, HasMaxLength, HasDefaultValueSql, HasIndex, HasCheckConstraint)
- Migration name: `<MigrationName>`

### Application Layer
New/modified files in Cex.Application:
- Command/Query record definition
- Handler class (injected dependencies listed)
- Validator (FluentValidation rules)
- DTOs

### API Layer (if applicable)
Controller, endpoints (method + route + request/response shape), authorization.

---

## Frontend Architecture (if applicable)

### Structure
Folder layout under `src/features/`.

### Routes

### Validation (Zod schema)

### API Integration
Custom React Query hooks following project patterns (useQuery / useMutation).

---

## Performance Considerations (if applicable)

Bullet list: indexes, bulk operations, query optimization, pre-computation strategies.

---

## Error Handling

| Scenario | Handling |
|---|---|
| ... | ... |

---

## Implementation Checklist

### Domain Layer
- [ ]

### Infrastructure Layer
- [ ]

### Application Layer
- [ ]

### API Layer (if applicable)
- [ ]

### Frontend (if applicable)
- [ ]

### Testing
- [ ]

---

## Technical Notes

Bullet list of important constraints, invariants, gotchas, or cross-cutting concerns
that implementers must know but that do not fit naturally into other sections.

---

## Database Migration (if applicable)

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Cex/Cex.Infrastructure/Cex.Infrastructure.csproj \
  --startup-project src/WebAPI/WebAPI.csproj \
  --context CexDbContext
```

---

## Related Features
- Feature name — brief relationship description

---

## Future
Brief bullet list of follow-on work intentionally out of scope for this feature.
```

---

Write the document now using the template above. Do not self-critique yet — just produce the best first draft you can.

---

## Phase 2 — Senior Engineer Review

Now switch roles: you are a senior software engineer reviewing the document you just wrote. Examine it critically for:

- **Correctness** — Are formulas, conditions, and logic correct? Are there off-by-one errors, wrong operators, or incorrect boundary conditions?
- **Ordering assumptions** — Does any code rely on sorted data without enforcing or documenting the sort?
- **Field / semantic conflicts** — Does the feature overwrite an existing field with a different meaning? Is that intentional and documented?
- **Performance** — Are expensive computations (e.g. per-batch per-signal calculations) unnecessarily repeated when they could be pre-computed?
- **Edge cases** — Empty batches, no candidates, partial failures, new records entering mid-run, pointer regression.
- **Missing context** — Are invariants, preconditions, or postconditions that implementers need to know left implicit?
- **Code sample correctness** — Would the code snippets compile and behave correctly? Check types, null handling, LINQ ordering, decimal vs int arithmetic.
- **EF Core conventions** — Do all DateTime/DateTime? columns have HasPrecision(0)? Do decimals have HasPrecision? Do strings have HasMaxLength?
- **Template compliance** — Are required sections present? Are optional sections included only when relevant?

List every finding as a concise bullet. Be specific: quote the exact line or section that has the issue and state what the fix should be.

---

## Phase 3 — Final Document

Apply every finding from Phase 2 to produce the final, corrected document. Follow the template structure exactly. Do not include the review findings inline — output only the clean, improved document.

Then write the final document to a file:
- Place it alongside the most closely related feature files in the codebase (e.g. next to the command handler it describes)
- Use the naming convention `<FEATURE_NAME_SCREAMING_SNAKE>.md` for Signal/background-job features
- Use `README.md` for CRUD/query features placed inside their feature folder
