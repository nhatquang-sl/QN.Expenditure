# Claude Code Instructions for QN.Expenditure

This file provides custom instructions for Claude Code when working on the QN.Expenditure project. For complete coding standards, see [.github/copilot-instructions.md](.github/copilot-instructions.md).

## Project Context

QN.Expenditure is a cryptocurrency expenditure tracking application with:
- **Backend**: .NET 8.0 with Clean Architecture (CQRS, MediatR, FluentValidation)
- **Frontend**: React + TypeScript + Material-UI + Vite
- **Database**: SQLite with Entity Framework Core

## Key Principles

### React Component Development

#### 1. Inline Handlers Preference
**Prefer inline handlers over useCallback** for most cases, especially with Material-UI components which are already optimized.

```typescript
// ✅ Preferred: Inline handler
<Select onChange={(e) => {
  navigate(`/path/${e.target.value}`);
  setPage(0);
}}>

// ❌ Avoid: Unnecessary useCallback
const handleChange = useCallback((e) => {
  navigate(`/path/${e.target.value}`);
}, [navigate])
<Select onChange={handleChange}>
```

**Only use useCallback when:**
- Passing to memoized child components (`React.memo()`)
- Used as dependencies in `useEffect`
- Profiling shows actual performance issues
- Working with large lists (hundreds/thousands of items)

#### 2. Extract Repetitive JSX
When you see duplicate JSX blocks (especially 50+ lines), extract them into reusable components.

```typescript
// ✅ Good: Extracted component
<StatisticsCard type="BUY" stats={formattedStats.buy} />
<StatisticsCard type="SELL" stats={formattedStats.sell} />

// ❌ Bad: 100+ lines of duplicated JSX for BUY/SELL cards
```

#### 3. Consistent Case Normalization
When normalizing string comparisons, **use toUpperCase() consistently** for both display and comparison.

```typescript
// ✅ Correct: Consistent uppercase
<Chip
  label={trade.side.toUpperCase()}
  color={trade.side.toUpperCase() === 'BUY' ? 'success' : 'error'}
/>

// ❌ Incorrect: Mixed casing
<Chip
  label={trade.side.toUpperCase()}
  color={trade.side.toLowerCase() === 'buy' ? 'success' : 'error'}
/>
```

#### 4. Use Modern JavaScript Methods
- Use `toSorted()` instead of `sort()` to avoid mutations
- Use `Set`/`Map` for O(1) lookups instead of arrays
- Use `useMemo()` for expensive computations

```typescript
// ✅ Good: Immutable sort
const availableSymbols = useMemo(() => {
  if (!syncSettings) return [];
  return syncSettings.map((s) => s.symbol).toSorted();
}, [syncSettings]);

// ❌ Bad: Mutating original array
return syncSettings.map((s) => s.symbol).sort();
```

#### 5. Hoist Constants Outside Components
Extract constants, format options, and static config outside the component to avoid recreation on every render.

```typescript
// ✅ Good: Hoisted constant
const DATE_FORMAT_OPTIONS: Intl.DateTimeFormatOptions = {
  year: 'numeric',
  month: 'long',
  day: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
};

export default function TradeHistory() {
  // Use DATE_FORMAT_OPTIONS here
}
```

### Backend Development

#### 1. Clean Architecture
- Follow Domain → Application → Infrastructure → Presentation layers
- Use CQRS pattern with MediatR
- Commands for writes, Queries for reads

#### 2. Naming Conventions
- **Entities**: `PascalCase` (e.g., `ExchangeSetting`)
- **DTOs**: Suffix with `Dto` (e.g., `ExchangeSettingDto`)
- **Commands**: Suffix with `Command` (e.g., `UpsertExchangeSettingCommand`)
- **Queries**: Suffix with `Query` (e.g., `GetExchangeSettingsQuery`)
- **Handlers**: Suffix with `Handler`
- **Validators**: Suffix with `Validator`

#### 3. Modern C# Features
- Use **file-scoped namespaces**: `namespace MyNamespace;`
- Use **primary constructors** for dependency injection (C# 12+)
- Use **record types** for DTOs and Commands/Queries
- Always use **async/await** with `CancellationToken`

```csharp
// ✅ Good: Modern C# patterns
namespace Cex.Application.Trade.Queries;

public record GetTradeHistoryQuery(string Symbol, int Page) : IRequest<TradeHistoryDto>;

public class GetTradeHistoryQueryHandler(
    ICurrentUser currentUser,
    ICexDbContext dbContext)
    : IRequestHandler<GetTradeHistoryQuery, TradeHistoryDto>
{
    public async Task<TradeHistoryDto> Handle(
        GetTradeHistoryQuery request,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### API Integration

#### 1. Use Generated Client
- Import from `store/api-client.ts` (NSwag generated)
- **Never modify** auto-generated API client files
- Wrap API calls in custom hooks

#### 2. React Query Patterns
```typescript
// ✅ Good: Custom hook wrapping API client
export function useGetTradeHistories(symbol: string, page: number, pageSize: number) {
  return useQuery({
    queryKey: ['trade-histories', symbol, page, pageSize],
    queryFn: () => tradeClient.getTradeHistories(symbol, page, pageSize),
    enabled: !!symbol,
    staleTime: 30000, // 30 seconds
  });
}
```

#### 3. Handle Loading & Error States
Always show loading indicators and user-friendly error messages.

### Performance Priorities

When optimizing React components, prioritize in this order:

1. **CRITICAL**: Eliminate data fetching waterfalls (use `Promise.all()`)
2. **HIGH**: Extract repetitive JSX into components
3. **MEDIUM**: Use `useMemo()` for expensive computations
4. **LOW**: Use `useCallback()` only when profiling shows issues

### File Organization

#### Frontend Structure
```
features/
└── trade/
    └── trade-history/
        ├── components/       # Reusable components
        │   └── StatisticsCard.tsx
        ├── hooks/            # Custom hooks
        │   ├── use-get-trade-histories.ts
        │   └── use-get-trade-statistics.ts
        ├── index.tsx         # Main component
        └── types.ts          # TypeScript types
```

#### Backend Structure
```
Cex.Application/
└── Trade/
    ├── Commands/
    │   └── SyncTradeHistory/
    │       ├── SyncTradeHistoryCommand.cs
    │       ├── SyncTradeHistoryCommandHandler.cs
    │       └── SyncTradeHistoryCommandValidator.cs
    └── Queries/
        └── GetTradeHistory/
            ├── GetTradeHistoryQuery.cs
            └── GetTradeHistoryQueryHandler.cs
```

## Code Review Checklist

When reviewing or writing code, check:

### React Components
- [ ] Are inline handlers used instead of unnecessary `useCallback`?
- [ ] Are repetitive JSX blocks extracted into components?
- [ ] Are expensive computations wrapped in `useMemo()`?
- [ ] Are constants hoisted outside the component?
- [ ] Is string comparison case-normalized consistently?
- [ ] Are custom hooks used for API calls?
- [ ] Are loading and error states handled?

### Backend Code
- [ ] Does it follow Clean Architecture layers?
- [ ] Are Commands/Queries using MediatR?
- [ ] Are validators using FluentValidation?
- [ ] Are modern C# features used (file-scoped namespace, primary constructors, records)?
- [ ] Is `CancellationToken` passed through async methods?
- [ ] Are entities properly configured in EF Core?

## Common Patterns

### Creating a New React Feature

1. Create feature folder: `src/features/[module]/[feature-name]/`
2. Create custom hooks in `hooks/` directory
3. Create reusable components in `components/` directory
4. Extract repetitive JSX into components
5. Hoist constants outside the main component
6. Use inline handlers for simple event handling
7. Use `useMemo()` for expensive computations

### Creating a New Backend Feature

1. Define entity in Domain layer
2. Create EF Core configuration in Infrastructure layer
3. Create DTO, Command/Query, Handler, and Validator in Application layer
4. Create controller endpoint in WebAPI
5. Regenerate TypeScript API client: `npm run generate-api-client`

## Anti-Patterns to Avoid

### React
- ❌ Using `useCallback` everywhere "just in case"
- ❌ Using `toLowerCase()` in one place and `toUpperCase()` in another for same data
- ❌ Duplicating large JSX blocks instead of extracting components
- ❌ Creating constants inside component functions
- ❌ Using `sort()` instead of `toSorted()` (causes mutations)
- ❌ Using `.includes()` on arrays for repeated lookups (use `Set` instead)

### Backend
- ❌ Putting business logic in controllers
- ❌ Using non-nullable reference types without enabling nullable context
- ❌ Not passing `CancellationToken` through async methods
- ❌ Using `.sort()` instead of `.OrderBy()` in LINQ queries
- ❌ Not validating commands with FluentValidation

## Quick Reference

**React Hooks Import Priority:**
1. `useState`, `useEffect`, `useMemo` (standard hooks)
2. Third-party hooks (`useQuery`, `useNavigate`)
3. Custom hooks (`useGetTradeHistories`)

**When to use `useCallback`:**
- Child component wrapped in `React.memo()`
- Dependency in `useEffect` or other hooks
- Profiling shows performance issue
- Otherwise: use inline handlers

**Performance Optimization Priority:**
1. Fix data fetching waterfalls
2. Extract repetitive components
3. Memoize expensive computations
4. Only then consider callback memoization

---

For complete coding standards and detailed examples, refer to [.github/copilot-instructions.md](.github/copilot-instructions.md).
