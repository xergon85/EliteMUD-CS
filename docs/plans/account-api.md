# Plan: Account API

_Created: Nov 18, 2026 · Status: in progress (slice 1)_

**Goal:** Players can manage their account outside the game: log in on a web page, see their characters, set and
confirm an email, and reset a forgotten password. A separate ASP.NET Core project (`EliteMud.Api`) runs next to the game
server on the same SQLite database, with a small React app (`web/`) on top.

Started as interview prep (Nov 2026); Day 1 and half of Day 2 of that plan were done on the `create-api` branch.

## Boundaries

- **The game server owns the schema.** Only the server calls `Migrate()`. The API refuses to start against a schema it
  doesn't know instead of migrating it.
- **The API never writes character data.** The server saves characters from memory and would overwrite it. The API
  may write account data (email, password hash, tokens): the server reads an account only at login.
- **Two processes, one SQLite file.** WAL mode and a busy timeout, so a reader doesn't block the writer.
- **No secrets in the repo.** The JWT signing key comes from configuration (user secrets in development).

## Slices

Each slice is one card and one PR. **Split:** Claude does the plumbing, you write the parts marked _(you)_.

### 1. Finish the skeleton

- Push the open commit; replace the template's `/weatherforecast` in `EliteMud.Api.http`.
- Upgrade `Microsoft.OpenApi` past 2.0.0 (NU1903); align `Microsoft.Extensions.DependencyInjection.Abstractions` with
  the EF Core version.
- The database path comes from configuration, not the working directory.
- The game server registers the database through `AddData()` instead of its own copy in `ServerBootstrap`.
- Tests: `AccountQueryService` _(you)_, and one endpoint test through `WebApplicationFactory` (harness by Claude).

### 2. Accounts in the database

- One migration (`docs/database-migrations.md`): `Account.EmailConfirmed`; email stored lowercase with a unique index
  filtered to non-null; new `AccountToken` (hashed token, `Purpose`: VerifyEmail / ResetPassword, expiry, used-at).
- Lowercase normalisation on save, and a check for existing emails that would clash before the index is added.
- WAL mode and busy timeout on both apps' connections.
- The characters query uses a `Select` projection instead of loading whole rows _(you)_.

### 3. Login

- `PasswordServiceAdapter` moves out of Server so the API can verify passwords too.
- `POST /auth/login` checks the password and returns a JWT _(you)_; JWT setup and signing-key config by Claude.
- `GET /accounts/{id}/characters` becomes `GET /me/characters`, reading the account from the token.
- Rate limiting on login; errors as `ProblemDetails` through one global handler.
- Tests: wrong password, inactive account, missing/expired token.

### 4. Email confirmation and password reset

- Set email → VerifyEmail token → confirm _(you)_.
- Forgot password takes a username or an email and sends only to a confirmed email; reset consumes the token
  _(you)_. Tokens are stored hashed, expire, and work once.
- Same response whether or not the account exists; rate limiting on forgot/reset.
- `IEmailSender` with a fake that logs the link (Claude).
- Tests: expiry, one-time use, unconfirmed email, unknown account gives the same response.

### 5. Web page

- Vite + React app in `web/` (scaffold, dev proxy and CORS by Claude).
- Pages: login, forgot password, reset, my characters _(you)_, with loading and error states.

### 6. Docs and CI

- README: game server ↔ database ↔ API ↔ web sketch, and how to run all three.
- `AGENTS.md` Run section, `docs/roadmap.md`.
- GitHub Actions: build and test the solution, build `web/`.

## Open questions

- Where does the password adapter go: Application (it already references Data) or a small shared project?
- Token lifetime for login (and whether to add refresh tokens, or just log in again).
- Does the game server's `account` login need to know about `EmailConfirmed` at all? (Probably not.)
