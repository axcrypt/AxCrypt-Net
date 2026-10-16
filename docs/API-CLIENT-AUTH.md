# How the client authenticates to the AxCrypt API

Every API call goes through `RestCaller` (`AxCrypt.Mono`). It signs requests with a **JWT Bearer token**; HTTP Basic is no longer used for normal calls.

## Flow

1. First call for an identity: `AuthTokenProvider` posts `{Email, Password}` to `/api/auth/login` and keeps the returned access token (15 min) and refresh token (30 days) **in memory only**.
2. Later calls reuse the access token. Within 30 s of expiry it is renewed with `/api/auth/refresh`; the server rotates the refresh token each time.
3. If the API answers `401`, `RestCaller` drops the access token and repeats the request once with a refreshed or newly issued one.
4. If the session is gone (server restart, revoked, expired) the provider signs in again with the identity's password.
5. Signing out (`KnownIdentities`) calls `/api/auth/logout` (3 s limit, never fails the sign-out) and forgets the tokens.

Nothing is persisted: after an app restart the first call signs in again with the password the app already holds.

## Explicit sign-in and sign-out (`AxAuthApiClient`)

Ordinary calls sign in by themselves. Use `AxAuthApiClient` where the result must be shown to the user:

| Call | Does |
|---|---|
| `LoginAsync()` | Verifies the credentials with the API right now (a cached session never vouches for a password) and starts a session. Returns a `SignInResult`: `Success`, `InvalidCredentials`, `AccountDisabled`, `RateLimited` (with `RetryAfter`) or `Unavailable`. |
| `AxAuthApiClient.EnsureSignedIn(result)` | Throws `UnauthorizedException` (`invalid_credentials` / `account_disabled`), `ApiException` 429, or `OfflineApiException` for the failures above. |
| `LogoutAsync()` | Ends this device's session (`/api/auth/logout`). Never throws when offline. |
| `LogoutEverywhereAsync()` | Ends all of the user's sessions (`/api/auth/logout-all`), falling back to this session only if that is refused. |

## Per-request authentication mode (`RestRequest.AuthMode`)

| Mode | Use |
|---|---|
| `Bearer` (default) | Everything signed-in. |
| `None` | Public endpoints and calls authorised by a code (e.g. set password with a verification code). No sign-in is attempted, so nothing counts towards account lockout. |
| `Basic` | Partner API-key endpoints only (`public/...`); the "password" is the API key. |

## Passphrase header

Only endpoints that unlock key material receive `X-AxCrypt-Passphrase` (Base64 of the password): `users/my/account…`, `usersv2/my/account…`, `secrets/get`, `secrets/save`. Add to `RestCaller.PassphraseEndpoints` if the API gains another.

## Failure handling

- A rejected password is not retried for 60 s (the same password); a different password is tried at once.
- Rate limits and server errors back off for 15 s (or the server's `Retry-After`).
- `429` surfaces as `ApiException` with `HttpStatusCode` 429; `ApiCaller.ErrorCode(response)` returns the API's machine-readable `code`.
- Error bodies are `application/problem+json`; they are accepted as JSON.

## Registering

`RuntimeEnvironment.RegisterTypeFactories()` registers `AuthTokenProvider`. Hosts without it (unit tests) get a no-op provider through `TokenProviders.Current`.

## Login and logout in the apps

- **Log on**: `LogOnAccountViewModel` calls `AxAuthApiClient.LoginAsync()` when online and reports `AccountDisabled` and `TooManyAttempts` as their own validation errors (`ValidationError`, mapped by `ToValidationMessage`). Other failures still end in the existing account check and show "Wrong Password".
- **Log off**: every path (menu sign-out, switching user, inactivity) goes through `KnownIdentities.SetDefaultEncryptionIdentity`, which revokes the server session and clears the local tokens.
- **Sign out everywhere**: `ProfileViewModel.SignOutEverywhereAsync()` (Mobile and Desktop shared projects; Windows, macOS and mobile apps) calls `logout-all`, then signs out locally. It has no menu entry yet.
- **Delete account**: the tokens are cleared after the account is deleted.
