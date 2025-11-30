# Exchange Configuration Feature

This feature allows users to securely store their exchange API credentials (ApiKey, Secret, and Passphrase) for multiple cryptocurrency exchanges.

## Architecture

The feature follows Clean Architecture principles with clear separation between layers:

### Domain Layer (`Cex.Domain`)
- **Entity**: `ExchangeConfig.cs` - Core domain entity with the following properties:
  - `UserId` (string) - User identifier
  - `ExchangeName` (string) - Name of the exchange (e.g., "Binance", "KuCoin", "Coinbase")
  - `ApiKey` (string) - Exchange API key
  - `Secret` (string) - Exchange API secret
  - `Passphrase` (string, nullable) - Optional passphrase (required for some exchanges like KuCoin)
  - `CreatedAt` (DateTime) - Creation timestamp
  - `UpdatedAt` (DateTime) - Last update timestamp

### Infrastructure Layer (`Cex.Infrastructure`)
- **Configuration**: `ExchangeConfigConfiguration.cs` - EF Core entity configuration with:
  - Composite primary key on (UserId, ExchangeName)
  - Index on UserId for efficient queries
  - Maximum length constraints for security
- **Database Context**: Updated `CexDbContext` with `ExchangeConfigs` DbSet
- **Migration**: `AddExchangeConfig.cs` - Database migration script

### Application Layer (`Cex.Application`)

#### DTOs
- **ExchangeConfigDto** - Data transfer object for API responses

#### Commands
1. **UpsertExchangeConfigCommand** - Create or update exchange configuration (upsert operation)
   ```csharp
   public record UpsertExchangeConfigCommand(
       string ExchangeName,
       string ApiKey,
       string Secret,
       string? Passphrase = null
   ) : IRequest<ExchangeConfigDto>;
   ```

2. **DeleteExchangeConfigCommand** - Delete an exchange configuration
   ```csharp
   public record DeleteExchangeConfigCommand(
       ExchangeName ExchangeName
   ) : IRequest;
   ```
   - Deletes the configuration if it exists
   - Idempotent operation (no error if not found)
   - User-scoped deletion (only deletes own configs)
   - Returns void, HTTP 204 No Content on success

#### Queries
1. **GetExchangeConfigsQuery** - Retrieve all exchange configurations for the current user
   ```csharp
   public record GetExchangeConfigsQuery : IRequest<List<ExchangeConfigDto>>;
   ```

## Usage Examples

### Upsert Exchange Configuration
```csharp
var command = new UpsertExchangeConfigCommand(
    ExchangeName: "KuCoin",
    ApiKey: "your-api-key",
    Secret: "your-secret",
    Passphrase: "your-passphrase"
);

var result = await mediator.Send(command);
```

### Get All Exchange Configurations
```csharp
var query = new GetExchangeConfigsQuery();
var configs = await mediator.Send(query);
```

### Delete Exchange Configuration
```csharp
var command = new DeleteExchangeConfigCommand(ExchangeName.Binance);
await mediator.Send(command);
// Returns nothing, HTTP 204 No Content on success
```

## Security Considerations

1. **Sensitive Data**: API keys, secrets, and passphrases are stored in the database. Consider:
   - Encrypting these fields at rest
   - Using SQL Server's Always Encrypted feature
   - Implementing additional encryption layer in the application

2. **Access Control**: All operations are scoped to the current user via `ICurrentUser` interface

3. **HTTPS**: Ensure all API endpoints use HTTPS to protect credentials in transit

4. **Validation**: Consider adding validation for:
   - Exchange name (whitelist of supported exchanges)
   - API key format validation
   - Rate limiting on configuration updates

## Database Schema

```sql
CREATE TABLE ExchangeConfigs (
    UserId nvarchar(36) NOT NULL,
    ExchangeName nvarchar(50) NOT NULL,
    ApiKey nvarchar(500) NOT NULL,
    Secret nvarchar(500) NOT NULL,
    Passphrase nvarchar(500) NULL,
    CreatedAt datetime2 NOT NULL,
    UpdatedAt datetime2 NOT NULL,
    CONSTRAINT PK_ExchangeConfigs PRIMARY KEY (UserId, ExchangeName)
);

CREATE INDEX IX_ExchangeConfigs_UserId ON ExchangeConfigs (UserId);
```

## Migration

To apply the migration to your database:

```bash
# Using the migration scripts (if already generated)
dotnet ef database update --project src/Cex/Cex.Infrastructure/Cex.Infrastructure.csproj --startup-project src/WebAPI/WebAPI.csproj --context CexDbContext

# Or generate the migration first
dotnet ef migrations add AddExchangeConfig --project src/Cex/Cex.Infrastructure/Cex.Infrastructure.csproj --startup-project src/WebAPI/WebAPI.csproj --context CexDbContext
```

## Future Enhancements

1. **Encryption**: Add field-level encryption for sensitive data
2. **Audit Logging**: Track when configurations are accessed or modified
3. **Configuration Validation**: Validate API keys by testing connection to exchange
4. **Multi-factor Authentication**: Require MFA for configuration changes
5. **Configuration Expiry**: Add expiry dates for API keys
6. **Key Rotation**: Automated key rotation reminders
