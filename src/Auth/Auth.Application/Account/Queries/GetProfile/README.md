# GetProfile Feature

## Overview

Returns the profile of the currently authenticated user. The handler reads directly from the `ICurrentUser` service (populated from JWT claims) — no database query is required. Used by the frontend to display and manage user account information after login.

## Data Model

### Entity: UserProfileDto (Existing)

| Field            | Type     | Notes                                     |
| ---------------- | -------- | ----------------------------------------- |
| `Id`             | `string` | Unique user identifier (ASP.NET Identity) |
| `Email`          | `string` | User's current email address              |
| `FirstName`      | `string` | User's first name                         |
| `LastName`       | `string` | User's last name                          |
| `EmailConfirmed` | `bool`   | Whether the user confirmed their email    |

### Business Rules

- The endpoint requires the user to be authenticated (`[Authorize]`).
- Profile data is sourced from JWT claims via `ICurrentUser` — no additional database lookup is needed.
- If the JWT is invalid or expired, the request is rejected with `401 Unauthorized`.

## Backend Architecture

### Domain Layer

No changes. `UserProfileDto` already satisfies the response shape.

### Infrastructure Layer

No changes. Profile data is resolved from JWT claims by the existing `CurrentUserService` bound to `ICurrentUser`.

### Application Layer

**Query:**

```csharp
// GetProfileQuery.cs
public record GetProfileQuery : IRequest<UserProfileDto>;
```

**Handler:**

```csharp
// GetProfileQueryHandler.cs
public class GetProfileQueryHandler(ICurrentUser currentUser)
    : IRequestHandler<GetProfileQuery, UserProfileDto>
{
    public Task<UserProfileDto> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = new UserProfileDto
        {
            Id             = currentUser.Id,
            Email          = currentUser.Email,
            FirstName      = currentUser.FirstName,
            LastName       = currentUser.LastName,
            EmailConfirmed = currentUser.EmailConfirmed,
        };

        return Task.FromResult(profile);
    }
}
```

No validator is needed — the query has no input parameters.

### Algorithm

1. Request arrives at `GET /api/auth/profile` with a valid Bearer token.
2. ASP.NET Core middleware validates the JWT and populates `ICurrentUser` from claims.
3. `GetProfileQueryHandler` reads all fields from `ICurrentUser` and maps them to `UserProfileDto`.
4. `UserProfileDto` is serialized and returned as the JSON response body.

### API Layer

| Method | Route               | Auth     | Request | Response         |
| ------ | ------------------- | -------- | ------- | ---------------- |
| `GET`  | `/api/auth/profile` | Required | (none)  | `UserProfileDto` |

```csharp
[Authorize]
[HttpGet("profile")]
[ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public Task<UserProfileDto> GetProfile()
    => _sender.Send(new GetProfileQuery());
```

## Performance Considerations

- No database I/O — data is read from in-memory JWT claims. Response is O(1).

## Error Handling

| Scenario             | Handling                                                         |
| -------------------- | ---------------------------------------------------------------- |
| Unauthenticated user | ASP.NET `[Authorize]` returns `401`                              |
| Expired JWT          | JWT middleware returns `401` before the query handler is reached |

## Implementation Checklist

### Backend

- [ ] Create `GetProfileQuery.cs` with query record and handler
- [ ] Add `GET /api/auth/profile` endpoint to `AuthController`
- [ ] Regenerate TypeScript API client (`npm run generate-api-client`)

### Testing

- [ ] Unit test: handler returns correct `UserProfileDto` from `ICurrentUser`
- [ ] Integration test: `GET /api/auth/profile` returns `200` with valid token, `401` without

### Frontend Integration

- [ ] Create `useGetProfile` hook using React Query
- [ ] Display profile data in account/settings page
- [ ] Handle `401` by redirecting to login

## Technical Notes

- `ICurrentUser` is populated by `CurrentUserService` in the Infrastructure layer, which reads claims from `IHttpContextAccessor`. It is registered as a scoped service.
- `UserProfileDto` implements `ICurrentUser`, so its properties align exactly with what is available in the JWT claims.
- No AutoMapper mapping is required — fields are assigned directly.

## Database Migration

No migration needed.

## Related Features

- [Login](../../../Commands/Login/) — issues the JWT that provides profile data via claims
- [ChangeEmail](../../../Commands/ChangeEmail/) — modifies the email reflected in the profile
- [ChangePassword](../../../Commands/ChangePassword/) — related account management command
- [GetUserLoginHistories](../GetUserLoginHistories/) — sibling query in the same module

## Future Enhancements

- Add `AvatarUrl`, `PhoneNumber`, or other extended profile fields as requirements grow.
- Consider caching the profile response (short TTL) if called at high frequency.
- Add an `UpdateProfile` command to allow editing `FirstName`/`LastName` directly.
