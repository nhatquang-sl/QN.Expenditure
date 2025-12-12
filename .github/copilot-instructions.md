# GitHub Copilot Instructions

## Project Overview

QN.Expenditure is a comprehensive cryptocurrency expenditure tracking application built with:
- **Backend**: .NET 8.0 with Clean Architecture (Domain, Application, Infrastructure layers)
- **Frontend**: React with TypeScript, Material-UI, and Vite
- **Database**: SQLite with Entity Framework Core
- **API**: RESTful endpoints with NSwag for TypeScript client generation
- **Architecture**: CQRS pattern using MediatR, FluentValidation for validation

## Project Structure

```
QN.Expenditure/
├── src/
│   ├── Auth/                      # Authentication module
│   │   ├── Auth.Domain/
│   │   ├── Auth.Application/
│   │   └── Auth.Infrastructure/
│   ├── Cex/                       # Cryptocurrency exchange module
│   │   ├── Cex.Domain/
│   │   ├── Cex.Application/
│   │   │   ├── Settings/          # Settings features (e.g., ExchangeSetting, SyncSetting)
│   │   │   └── Trade/             # Trade features (e.g., SyncTradeHistory)
│   │   └── Cex.Infrastructure/
│   ├── Libs/                      # Shared libraries
│   │   ├── Lib.Application/
│   │   ├── Lib.ExternalServices/
│   │   └── Lib.Notifications/
│   ├── WebAPI/                    # ASP.NET Core Web API
│   └── WebUI.React/               # React frontend
│       └── src/
│           └── features/
│               ├── settings/      # Settings features (e.g., exchange-setting, sync-setting)
│               └── trade-history/ # Trade history features (e.g., sync, display)
├── scripts/                        # Shell scripts for development
└── .github/                       # GitHub configuration
```

## Feature Organization

When creating new features, organize them consistently between backend and frontend:

### Backend Feature Structure (Application Layer)

**Settings Module Example:**
```
Cex.Application/
└── Settings/                      # Feature category
    ├── ExchangeSetting/           # Exchange credentials
    │   ├── Commands/
    │   ├── Queries/
    │   └── DTOs/
    └── SyncSetting/               # Sync configuration
        ├── Commands/
        ├── Queries/
        └── DTOs/
```

**Trade Module Example:**
```
Cex.Application/
└── Trade/                         # Feature category
    ├── Commands/
    │   └── SyncTradeHistory/      # Sync trade history from exchanges
    │       ├── SyncTradeHistoryCommand.cs
    │       ├── SyncTradeHistoryCommandHandler.cs
    │       └── README.md
    └── Queries/
        └── GetTradeHistory/       # Query/display trade history (future)
            └── GetTradeHistoryQuery.cs
```

### Frontend Feature Structure

**Settings Module Example:**
```
features/
└── settings/                      # Feature category (matches backend)
    ├── exchange-setting/          # Feature name (kebab-case)
    │   ├── hooks/
    │   ├── list/
    │   ├── create/
    │   ├── update/
    │   ├── index.tsx
    │   ├── form.tsx
    │   └── types.ts
    └── sync-setting/              # Sync configuration (kebab-case)
        ├── hooks/
        ├── list/
        ├── create/
        ├── update/
        ├── index.tsx
        ├── form.tsx
        └── types.ts
```

**TradeHistory Module Example:**
```
features/
└── trade-history/                 # Feature category (matches backend)
    ├── sync/                      # Sync trade history feature
    │   ├── hooks/
    │   └── index.tsx
    └── display/                   # Display trade history feature (future)
        ├── hooks/
        ├── list/
        ├── index.tsx
        └── types.ts
```

## Coding Standards

### Backend (.NET/C#)

#### Architecture Patterns
- **Follow Clean Architecture**: Domain → Application → Infrastructure → Presentation
- **Use CQRS**: Separate Commands (write) from Queries (read) using MediatR
- **Implement Repository Pattern**: Abstract data access through interfaces
- **Use Dependency Injection**: Constructor injection for all dependencies

#### Naming Conventions
- **Entities**: PascalCase (e.g., `ExchangeSetting`, `SpotGrid`)
- **DTOs**: Suffix with `Dto` (e.g., `ExchangeSettingDto`)
- **Commands**: Suffix with `Command` (e.g., `UpsertExchangeSettingCommand`)
- **Queries**: Suffix with `Query` (e.g., `GetExchangeSettingsQuery`)
- **Handlers**: Suffix with `Handler` (e.g., `UpsertExchangeSettingCommandHandler`)
- **Validators**: Suffix with `Validator` (e.g., `UpsertExchangeConfigCommandValidator`)
- **Controllers**: Suffix with `Controller`, use kebab-case routes (e.g., `/api/exchange-configs`)

#### Code Style
- Use **file-scoped namespaces**: `namespace MyNamespace;`
- Use **primary constructors** for dependency injection (C# 12+)
- Use **record types** for DTOs and Commands/Queries
- Use **nullable reference types**: Enable in all projects
- Use **async/await**: Always for I/O operations
- Use **CancellationToken**: Pass through all async methods

#### Entity Framework Core
- **Use configurations**: Separate entity configuration in `EntityTypeConfiguration<T>` classes
- **Composite keys**: Use `HasKey(x => new { x.Property1, x.Property2 })`
- **Enum storage**: Use `.HasConversion<string>()` for readable database values
- **Migrations**: Name with descriptive verbs (e.g., `AddExchangeConfig`)

#### Validation
- **Use FluentValidation**: Create separate validator classes
- **Validate at boundaries**: Commands should have validators
- **Custom error messages**: Provide user-friendly validation messages
- **Enum validation**: Use `.IsInEnum()` for enum properties

#### Error Handling
- **Use exceptions**: For exceptional scenarios only
- **Custom exceptions**: Create domain-specific exceptions when needed
- **Validation exceptions**: Return `UnprocessableEntityException` for validation errors
- **Not found**: Return appropriate HTTP status codes (404, 422, etc.)

### Frontend (React/TypeScript)

#### File Organization
- **Feature-based structure**: Group by feature (e.g., `features/bnb/setting/`)
- **Component files**: One component per file
- **Co-locate related files**: types, hooks, components in same directory

#### Naming Conventions
- **Components**: PascalCase (e.g., `ExchangeConfigForm`, `BnbSetting`)
- **Hooks**: Prefix with `use` (e.g., `useGetExchangeConfigs`, `useUpsertExchangeConfig`)
- **Types/Interfaces**: PascalCase (e.g., `ExchangeConfigData`, `Column`)
- **Constants**: UPPER_SNAKE_CASE (e.g., `EXCHANGE_OPTIONS`)
- **Functions**: camelCase (e.g., `handleSubmit`, `onSubmit`)

#### TypeScript Patterns
- **Use types over interfaces**: Prefer `type` for simple structures
- **Zod for validation**: Use Zod schemas for form validation
- **Infer types**: Use `z.infer<typeof Schema>` for Zod schemas
- **Use enums**: Import enums from auto-generated API client
- **Avoid `any`**: Use proper typing or `unknown` when necessary

#### React Patterns
- **Functional components**: Always use function components with hooks
- **Custom hooks**: Extract reusable logic into custom hooks
- **React Query**: Use for server state management (queries/mutations)
- **React Hook Form**: Use for form state management with Zod validation
- **Material-UI**: Use MUI components for UI consistency

#### API Integration
- **Use generated client**: Import from `store/api-client.ts` (NSwag generated)
- **Don't modify generated code**: Auto-generated files should not be edited
- **Use hooks for API calls**: Wrap API calls in custom hooks
- **Handle loading states**: Always show loading indicators
- **Handle errors**: Display user-friendly error messages

## Common Patterns

### Creating a New Feature (Backend)

1. **Domain Layer**:
   ```csharp
   // Entity
   public class MyEntity
   {
       public string Id { get; set; } = string.Empty;
       public string UserId { get; set; } = string.Empty;
       // Other properties
   }
   ```

2. **Infrastructure Layer**:
   ```csharp
   // Configuration
   public class MyEntityConfiguration : IEntityTypeConfiguration<MyEntity>
   {
       public void Configure(EntityTypeBuilder<MyEntity> builder)
       {
           builder.HasKey(x => new { x.UserId, x.Id });
           // Other configurations
       }
   }
   ```

3. **Application Layer**:
   ```csharp
   // DTO
   public record MyEntityDto(string Id, string Name);

   // Command
   public record CreateMyEntityCommand(string Name) : IRequest<MyEntityDto>;

   // Handler
   public class CreateMyEntityCommandHandler(
       ICurrentUser currentUser,
       IMyDbContext dbContext)
       : IRequestHandler<CreateMyEntityCommand, MyEntityDto>
   {
       public async Task<MyEntityDto> Handle(
           CreateMyEntityCommand request,
           CancellationToken cancellationToken)
       {
           // Implementation
       }
   }

   // Validator
   public class CreateMyEntityCommandValidator : AbstractValidator<CreateMyEntityCommand>
   {
       public CreateMyEntityCommandValidator()
       {
           RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
       }
   }
   ```

4. **API Controller**:
   ```csharp
   [Route("api/my-entities")]
   [ApiController]
   public class MyEntitiesController(ISender sender) : ControllerBase
   {
       [HttpPost]
       public Task<MyEntityDto> Create([FromBody] CreateMyEntityCommand request)
           => sender.Send(request);
   }
   ```

### Creating a New Feature (Frontend)

1. **Create feature folder**: `src/features/my-feature/`

2. **Define types**: `types.ts`
   ```typescript
   import { z } from 'zod';

   export const MySchema = z.object({
       name: z.string().min(1).max(100),
   });

   export type MyData = z.infer<typeof MySchema>;
   ```

3. **Create custom hooks**: `hooks/use-create-my-entity.ts`
   ```typescript
   import { useMutation, useQueryClient } from '@tanstack/react-query';
   import { myEntityClient } from 'store/api-client';

   export function useCreateMyEntity() {
       const queryClient = useQueryClient();
       return useMutation({
           mutationFn: (command) => myEntityClient.create(command),
           onSuccess: () => {
               queryClient.invalidateQueries(['my-entities']);
           },
       });
   }
   ```

4. **Create component**: `index.tsx`
   ```typescript
   import { useForm } from 'react-hook-form';
   import { zodResolver } from '@hookform/resolvers/zod';

   export default function MyFeature() {
       const { handleSubmit } = useForm({
           resolver: zodResolver(MySchema),
       });

       // Component implementation
   }
   ```

## Testing Guidelines

### Backend Testing
- **Unit tests**: Test handlers, validators in isolation
- **Integration tests**: Test database operations with in-memory database
- **Use xUnit**: Standard testing framework
- **Use Shouldly**: For fluent assertions
- **Name tests clearly**: `MethodName_Scenario_ExpectedResult`

### Frontend Testing
- **Component tests**: Test user interactions
- **Hook tests**: Test custom hooks in isolation
- **Mock API calls**: Use MSW or similar for API mocking

## Database Migrations

### Creating Migrations
```bash
dotnet ef migrations add MigrationName \
  --project src/Cex/Cex.Infrastructure/Cex.Infrastructure.csproj \
  --startup-project src/WebAPI/WebAPI.csproj \
  --context CexDbContext
```

### Applying Migrations
```bash
dotnet ef database update \
  --project src/Cex/Cex.Infrastructure/Cex.Infrastructure.csproj \
  --startup-project src/WebAPI/WebAPI.csproj \
  --context CexDbContext
```

## API Client Generation

After modifying API controllers, regenerate TypeScript client:
```bash
# From project root
npm run generate-api-client
```

## Security Considerations

- **Never commit secrets**: Use user secrets or environment variables
- **Validate user input**: Always validate at API boundaries
- **Use user scoping**: Filter data by `ICurrentUser.Id`
- **Encrypt sensitive data**: Consider field-level encryption for API keys/secrets
- **Use HTTPS**: Always in production
- **Implement rate limiting**: For sensitive operations

## Common Commands

### Development
```bash
# Run backend
dotnet run --project src/WebAPI/WebAPI.csproj

# Run frontend
cd src/WebUI.React && npm run dev

# Run tests
dotnet test

# Format code
dotnet format
```

### Docker
```bash
# Build and run with Docker Compose
docker-compose up --build
```

## Additional Notes

- **Prefer composition over inheritance**
- **Keep methods small and focused** (single responsibility)
- **Write self-documenting code** (clear names over comments)
- **Use meaningful variable names**
- **Avoid magic numbers/strings** (use constants)
- **Handle null appropriately** (use nullable types)
- **Log errors appropriately** (structured logging)
- **Document public APIs** (XML comments for public methods)
- **Prefer `List<T>` over `IEnumerable<T>`**: Use concrete `List<T>` for parameters when you need to access `.Count` property
- **Use `.Count` instead of `.Any()`**: For materialized collections (like `List<T>`), use `.Count == 0` or `.Count > 0` instead of `.Any()` for better performance and clarity

## When in Doubt

1. Check existing similar features for patterns
2. Follow Clean Architecture principles
3. Keep it simple (YAGNI - You Aren't Gonna Need It)
4. Make it work, make it right, make it fast (in that order)
5. Ask for code review when implementing new patterns
