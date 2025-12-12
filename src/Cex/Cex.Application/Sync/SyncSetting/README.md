# Sync Setting Feature

## Overview
Manages synchronization configurations for cryptocurrency trading symbols. Tracks start and last sync timestamps per symbol per user.

**Module Location**: `Cex.Application/Sync/`
- `SyncSetting/` - Configuration for sync operations
- `SyncTradeHistory/` - Trade history synchronization (uses SyncSetting)

## Data Model

### Entity: SyncSetting
```csharp
public class SyncSetting
{
    public string UserId { get; set; } = string.Empty;  // Composite key
    public string Symbol { get; set; } = string.Empty;  // Composite key, max 50 chars
    public DateTime StartSync { get; set; }
    public DateTime LastSync { get; set; }              // Auto-set on create, updated by system
}
```

**Composite Key**: `(UserId, Symbol)`  
**Table Name**: `SyncSettings`

### DTO: SyncSettingDto
```csharp
public class SyncSettingDto
{
    public string Symbol { get; set; }
    public long StartSync { get; set; }    // Unix timestamp in milliseconds
    public long LastSync { get; set; }     // Unix timestamp in milliseconds
}
```

### Business Rules
1. **Create**: 
   - User provides: Symbol, StartSync
   - System sets: LastSync = StartSync (automatically)
2. **Update**:
   - User can modify: StartSync
   - If StartSync changes: LastSync is updated to match StartSync
   - If StartSync unchanged: LastSync remains unchanged

## Backend Architecture

### Domain Layer
- `Cex.Domain/Entities/SyncSetting.cs` - Entity definition

### Infrastructure Layer
- `Cex.Infrastructure/Data/Configurations/SyncSettingConfiguration.cs`
  - Composite key: `builder.HasKey(x => new { x.UserId, x.Symbol })`
  - Max lengths: UserId (36), Symbol (50)
  - Index on UserId

### Application Layer
Location: `Cex.Application/Settings/SyncSetting/`

**DTOs**:
- `DTOs/SyncSettingDto.cs`

**Commands**:
- `Commands/UpsertSyncSetting/UpsertSyncSettingCommand.cs`
  - Input: Symbol (string), StartSync (long - Unix timestamp in milliseconds)
  - Converts timestamp to DateTime internally
  - Create logic: Sets LastSync = StartSync
  - Update logic: If StartSync changes, updates both StartSync and LastSync to new value
- `Commands/UpsertSyncSetting/UpsertSyncSettingCommandValidator.cs`
  - Symbol: required, max 50 characters
  - StartSync: must be greater than 0 (valid timestamp)
- `Commands/DeleteSyncSetting/DeleteSyncSettingCommand.cs`
  - Idempotent delete by symbol

**Queries**:
- `Queries/GetSyncSettings/GetSyncSettingsQuery.cs`
  - Filters by current user ID
  - Orders by Symbol ascending

### API Layer
**Controller**: `WebAPI/Controllers/SyncSettingsController.cs`

**Endpoints**:
- `GET /api/sync-settings` → Returns `List<SyncSettingDto>`
  - StartSync and LastSync returned as Unix timestamps (milliseconds)
- `POST /api/sync-settings` → Upsert, returns `SyncSettingDto`
  - Request body: `{ symbol: string, startSync: long }` (timestamp in milliseconds)
  - Create: Sets LastSync = StartSync (converts to DateTime internally)
  - Update: Only updates StartSync
  - Response contains timestamps as long (milliseconds)
- `DELETE /api/sync-settings/{symbol}` → Returns `204 No Content`

**Note**: All datetime communication between frontend and backend uses Unix timestamps (milliseconds). Backend converts timestamps to DateTime for database storage.

## Frontend Architecture

### Structure
Location: `src/features/settings/sync-setting/`

```
sync-setting/
├── types.ts                    # Zod schema and types
├── form.tsx                    # Form component
├── index.tsx                   # Main page with Outlet
├── hooks/
│   ├── use-get-sync-settings.ts
│   ├── use-upsert-sync-setting.ts
│   └── use-delete-sync-setting.ts
├── list/
│   ├── index.tsx              # Table component
│   ├── item.tsx               # Table row with actions
│   └── types.ts               # Column definitions
├── create/
│   └── index.tsx              # Create page
└── update/
    └── index.tsx              # Update page
```

### Routes
- `/settings/sync-setting` - Create form + table
- `/settings/sync-setting/{symbol}` - Update form + table

### Validation (Zod)
```typescript
const SyncSettingSchema = z.object({
  symbol: z.string().min(1).max(50),
  startSync: z.date(),
  // Note: lastSync not in form, managed by backend
  // API returns timestamps as long (milliseconds), converted to Date in frontend
});
```

### Form Fields
**Create Mode**:
- Symbol (text input, required)
- Start Sync (datetime input, required)

**Update Mode**:
- Symbol (display only, read-only)
- Start Sync (datetime input, editable)
- Last Sync (display only, read-only)

### Table Columns
- Symbol
- Start Sync (formatted datetime)
- Last Sync (formatted datetime, system-managed)
- Actions (Edit/Delete buttons)

### API Integration
```typescript
// Store client
const syncSettingsClient = new SyncSettingsClient(API_ENDPOINT, instance);

// React Query hooks
useGetSyncSettings()           // Query key: ['syncSettings']
useUpsertSyncSetting()         // Invalidates: ['syncSettings']
useDeleteSyncSetting()         // Invalidates: ['syncSettings'], navigates to /settings/sync-setting
```

## Implementation Checklist

### Backend
- [ ] Create `SyncSetting` entity in Domain
- [ ] Create `SyncSettingConfiguration` in Infrastructure
- [ ] Add `DbSet<SyncSetting>` to ICexDbContext and CexDbContext
- [ ] Create migration: `dotnet ef migrations add SyncSetting`
- [ ] Implement `SyncSettingDto`
- [ ] Implement `UpsertSyncSettingCommand` with validator
- [ ] Implement `DeleteSyncSettingCommand`
- [ ] Implement `GetSyncSettingsQuery`
- [ ] Create `SyncSettingsController`
- [ ] Write integration tests

### Frontend
- [ ] Create feature folder structure
- [ ] Implement `types.ts` with Zod schema
- [ ] Implement hooks (get, upsert, delete)
- [ ] Implement `form.tsx`
- [ ] Implement `list/` components (index, item, types)
- [ ] Implement `create/index.tsx`
- [ ] Implement `update/index.tsx`
- [ ] Implement `index.tsx` main page
- [ ] Add `SyncSettingsClient` to store
- [ ] Add routes to App.tsx
- [ ] Regenerate API client: `npm run generate-api-client`

## Technical Notes
- Part of Sync module alongside SyncTradeHistory
- Composite key ensures one sync setting per symbol per user
- Upsert pattern: updates if exists, creates if not
- Delete is idempotent (no error if not found)
- All operations scoped to current user via ICurrentUser.Id
- SyncTradeHistory feature will load these settings to perform trade history synchronization

### LastSync Management
- **Create**: Backend automatically sets `LastSync = StartSync`
- **Update**: If `StartSync` changes, backend updates `LastSync = StartSync` (resets sync position)
- **Update**: If `StartSync` unchanged, `LastSync` remains unchanged
- **Sync Process**: System updates `LastSync` during actual synchronization operations
- **Frontend**: `LastSync` is read-only, never editable by user

### Command Logic
```csharp
// Convert Unix timestamp (milliseconds) to DateTime
var startSyncDateTime = DateTimeOffset.FromUnixTimeMilliseconds(request.StartSync).UtcDateTime;

// Create
if (entity == null)
{
    entity = new SyncSetting
    {
        UserId = currentUser.Id,
        Symbol = request.Symbol,
        StartSync = startSyncDateTime,
        LastSync = startSyncDateTime  // Auto-set to StartSync
    };
    cexDbContext.SyncSettings.Add(entity);
}
// Update
else
{
    // Check if StartSync actually changed
    if (entity.StartSync != startSyncDateTime)
    {
        entity.StartSync = startSyncDateTime;
        entity.LastSync = startSyncDateTime;  // Reset LastSync to match new StartSync
    }
    cexDbContext.SyncSettings.Update(entity);
}
```

**Update Logic Rationale**: When a user changes StartSync (deciding to sync from a different starting date), LastSync is also reset to that new starting point. This maintains consistency and allows the sync process to restart from the new StartSync value. If StartSync is unchanged, no updates occur.

### Frontend-Backend Communication
- **Request**: Frontend sends `startSync` as Unix timestamp (milliseconds) using `date.getTime()`
- **Response**: Backend returns `startSync` and `lastSync` as Unix timestamps (milliseconds)
- **Conversion**: Backend converts timestamp to `DateTime` for database storage, converts back to timestamp in DTO
