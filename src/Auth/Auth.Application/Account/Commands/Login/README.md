# Login Feature

## Overview

The Login feature authenticates a user using email/password, generates JWT access and refresh tokens, stores login activity, and returns authenticated user profile data. The authentication model is cookie-first: tokens are stored in secure HttpOnly cookies and are not exposed in the JSON response body.

## Data Model

### Entity: UserLoginHistory (Existing)

| Field        | Type     | Notes                                               |
| ------------ | -------- | --------------------------------------------------- |
| Id           | long     | Auto-increment primary key for login history record |
| UserId       | string   | Authenticated user identifier                       |
| IpAddress    | string?  | Captured from HttpContext connection                |
| UserAgent    | string?  | Captured from request headers                       |
| AccessToken  | string   | Generated JWT access token                          |
| RefreshToken | string   | Generated JWT refresh token                         |
| CreatedAt    | DateTime | UTC creation timestamp                              |

### Business Rules

- Email and password must pass `LoginCommandValidator` checks.
- Authentication uses ASP.NET Identity `PasswordSignInAsync`.
- Successful login and `IsNotAllowed` both proceed to token generation in current implementation.
- A login history record is persisted for every successful command execution.
- Access and refresh tokens are generated with different expirations.
- `UserAuthDto` token fields are marked with `JsonIgnore`, so token values are not exposed in JSON response body.
- API controller writes tokens into secure HttpOnly cookies (`accessToken`, `refreshToken`).
- Client should not store tokens in localStorage/sessionStorage.
- Client should not send `Authorization: Bearer` header for normal authenticated requests in this model.
- Cross-origin frontend requests must include credentials so browser sends cookies.

## Backend Architecture

### Domain Layer

- Reuses existing `UserLoginHistory` entity for audit trail.

### Infrastructure Layer

- `IdentityService.LoginAsync` validates credentials and returns `UserProfileDto`.
- `JwtProvider.GenerateTokens` creates signed access/refresh JWTs with user claims:
  - `id`, `emailCus`, `firstName`, `lastName`, `emailConfirmed`, `type`
- `JwtProvider` expiration settings:
  - Access token: `UtcNow + 1 minute`
  - Refresh token: `UtcNow + 24 hours`

### Application Layer

- `LoginCommand : IRequest<UserAuthDto>`
- `LoginCommandHandler` flow:
  1. Calls `IIdentityService.LoginAsync`
  2. Calls `IJwtProvider.GenerateTokens`
  3. Maps profile to `UserAuthDto`
  4. Assigns generated tokens to DTO (for controller cookie setup)
  5. Persists `UserLoginHistory`
  6. Calls `SaveChangesAsync`
- `LoginCommandValidator` enforces email format and password complexity with unified error message.

### Algorithm

1. Client sends `POST /api/auth/login` with email/password/rememberMe.
2. Controller enriches command with IP address and user-agent.
3. MediatR dispatches `LoginCommand`.
4. Identity layer validates credentials and loads user profile.
5. JWT provider generates access and refresh tokens.
6. Handler records login history with token snapshot and request metadata.
7. Handler returns `UserAuthDto`.
8. Controller appends secure HttpOnly cookies for both tokens.
9. Response body returns profile fields (tokens ignored by JSON serializer).

### API Layer

- Endpoint: `POST /api/auth/login`
- Request body: `LoginCommand`
  - `email`, `password`, `rememberMe`
- Response:
  - `200 OK`: `UserAuthDto` profile payload (without token fields in JSON)
  - `400 Bad Request`: invalid credentials/input
- Side effect: sets cookies
  - `accessToken` (HttpOnly, Secure, SameSite=None)
  - `refreshToken` (HttpOnly, Secure, SameSite=None)

Cookie-only authentication behavior for subsequent API calls:

- Browser automatically attaches cookies when request uses credentials mode.
- Frontend must call API with credentials enabled (for example: `credentials: "include"` in fetch).
- Backend CORS must allow credentials and explicit origins (not wildcard `*`).

## Performance Considerations

- Login is dominated by Identity authentication and token generation (constant-time per request).
- Database write for `UserLoginHistory` adds one insert per successful login.
- Current access token lifetime is very short (1 minute), which may increase refresh frequency.

## Error Handling

- Validation failure: `LoginCommandValidator` rejects malformed email/password.
- Invalid credentials: `BadRequestException("Email or Password incorrect.")`.
- Two-factor required: `BadRequestException("Requires Two Factor.")`.
- Account locked: `BadRequestException("User account locked out.")`.
- User not found after sign-in success path: `NotFoundException`.
- API surface maps failures to 400/other configured exception middleware responses.
- Missing cookie on protected endpoints: `401 Unauthorized`.
- Cross-site request without credentials mode enabled: request reaches API without auth cookie and returns `401 Unauthorized`.

## Implementation Checklist

### Backend

- [x] `LoginCommand` and `LoginCommandHandler` implemented
- [x] `LoginCommandValidator` implemented
- [x] `POST /api/auth/login` endpoint implemented
- [x] JWT token generation via `IJwtProvider` implemented
- [x] Login history persistence implemented
- [x] Access token and refresh token are set as HttpOnly cookies in `AuthController`
- [ ] Confirm CORS policy allows credentials and configured frontend origins
- [ ] Ensure logout endpoint clears auth cookies (`accessToken`, `refreshToken`)
- [ ] API client regenerated (`npm run generate-api-client`) when contract changes

### Testing

- [ ] Unit tests for `LoginCommandHandler` success path and exceptions
- [ ] Unit tests for `LoginCommandValidator` rules
- [ ] Integration test for controller cookie behavior and response payload

### Frontend Integration

- [ ] Ensure login mutation calls `POST /api/auth/login`
- [ ] Read authenticated user profile from response body
- [ ] Rely on HttpOnly cookies for token storage/session continuity
- [ ] Remove token persistence in localStorage/sessionStorage
- [ ] Remove Authorization header injection logic from HTTP client interceptors
- [ ] Send requests with credentials enabled (`withCredentials` / `credentials: "include"`)
- [ ] Handle 400 responses with user-friendly messages
- [ ] Handle 401 globally by redirecting to login and/or triggering refresh flow

## Technical Notes

- `UserAuthDto` inherits `UserProfileDto` and includes token properties only for server-side flow.
- Because token properties are `[JsonIgnore]`, controller can use them but clients do not receive them in JSON.
- Cookies are set in `AuthController`, not in command handler, keeping infrastructure concerns in API layer.
- Login command currently imports `Lib.Application.Logging` but does not use it.

Migration from header-based tokens to cookie-only tokens:

1. Backend

- Keep issuing tokens in command handler, but expose them only through cookies.
- Verify authentication middleware can read token from cookie for protected endpoints.
  - Update JwtBearer token extraction in `src/Auth/Auth.Infrastructure/Identity/JwtBearerSetup.cs`:
    - Add `options.Events.OnMessageReceived` and set `context.Token` from request cookie `accessToken`.
    - Optional migration fallback: if cookie is missing, read token from `Authorization: Bearer` header for temporary backward compatibility.
  - Suggested implementation:

    ```csharp
    using Microsoft.Net.Http.Headers;

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var cookieToken = context.Request.Cookies["accessToken"];
            if (!string.IsNullOrWhiteSpace(cookieToken))
            {
                context.Token = cookieToken;
                return Task.CompletedTask;
            }

            // Optional compatibility mode while migrating old frontend clients.
            var authorization = context.Request.Headers[HeaderNames.Authorization].ToString();
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Token = authorization["Bearer ".Length..].Trim();
            }

            return Task.CompletedTask;
        }
    };
    ```

  - Keep token validation settings unchanged (issuer, audience, key, lifetime) because only the token source changes.
  - Confirm middleware order in `src/WebAPI/Program.cs` remains:
    - `UseCors()`
    - `UseAuthentication()`
    - `UseAuthorization()`
  - Validation checklist:
    1. Call login endpoint and confirm response sets `Set-Cookie` for `accessToken` and `refreshToken`.
    2. Call authorized endpoint (for example `GET /api/auth/profile`) without Authorization header but with browser credentials enabled.
    3. Expect `200 OK` when cookie exists; `401 Unauthorized` after deleting cookie.
    4. If fallback mode is enabled, verify both cookie-token and header-token requests are accepted during migration.
  - Common pitfalls:
    - Cross-origin requests without credentials mode do not send cookies, causing `401`.
    - `SameSite=None` requires `Secure=true`; otherwise browsers may drop cookies.
    - Using `WithOrigins("*")` with credentials is invalid; use explicit origins only.

- Configure CORS with `AllowCredentials()` and explicit allowed origins.

1. Frontend

- Delete all localStorage token read/write logic.
- Delete Authorization Bearer token injection logic.
- Ensure API client always sends credentials.

1. Security

- Add CSRF protection strategy for state-changing endpoints when using cookie auth.
- Keep `Secure=true` and consider environment-specific `SameSite` behavior for local development.

## Database Migration

No migration required for this feature (existing schema supports login history).

## Related Features

- Register: account creation and initial identity bootstrap.
- RefreshToken: token renewal flow after access token expiry.
- GetProfile: reads authenticated user profile from current claims.
- GetUserLoginHistories: query endpoint for login audit records.

## Future Enhancements

- Add refresh-token rotation and revocation strategy.
- Consider hashing or encrypting stored token values in login history.
- Support and document 2FA login path with dedicated API contract.
- Revisit access token lifetime to balance security and UX.
- Add rate limiting / brute-force protection signals at API boundary.
