# Plan: user accounts (register, sign in, password reset)

Status: implemented (branch `feature/accounts`). Decided with the owner: Google sign in and email/password together, development on
`localtest.me`, optional email domain allow list (`Accounts:AllowedEmailDomains`), no migration of demo identities, display name
chosen by the person at the first sign in (Google only suggests it). The sections below are the original plan; see
`installation.md` ("Accounts") for how it works and is configured. Differences: tokens are random single use values whose hash is
stored in MongoDB (no data protection tokens), the user store is a small repository on the driver instead of an ASP.NET Core
Identity store, Google sign in is in scope.

## Where we are

- `Identity.API` (Duende IdentityServer 7) has no user database. `DemoUserProvider` accepts any alphanumeric
  username (< 12 characters) with any password; the subject id is the hex encoding of the username.
- MongoDB only stores meeting data (`Conference`, `ConferenceLink`). Conferences, moderators and links refer to people
  by that subject id.
- `ProfileService` puts the username into the `name` claim, which the app shows as the participant name.

## Goal

People can create an account with email and password, confirm their email, sign in, and reset a forgotten password,
with the same look as the rest of the app. Local development, CI and the Cypress tests keep working without email.

## Decisions

| Topic | Decision | Why |
| --- | --- | --- |
| Mode | `Identity:Mode` = `Demo` (default in development and e2e) or `Accounts` | keeps the one-click demo and the existing tests, production opts in |
| User store | ASP.NET Core Identity with a small custom MongoDB store (`IUserStore`, `IUserPasswordStore`, `IUserEmailStore`, `IUserLockoutStore`) on the existing MongoDB | no new database, no dependency on a community package that lags behind `MongoDB.Driver` |
| Subject id | new accounts get a random id (ObjectId string), demo users keep the hex scheme | no migration of existing conferences, the two schemes cannot collide (hex of a username vs. a 24 character ObjectId) |
| Display name | separate from the email, chosen at registration, goes into the `name` claim | email addresses must not appear in meetings |
| Passwords | length >= 10, no composition rules, hashed by ASP.NET Core Identity (PBKDF2), lockout after 5 failures for 15 min | current NIST guidance |
| Email | `IEmailSender` abstraction, MailKit SMTP sender configured via `Email:*`, Mailpit container in `docker-compose.dev.yml` | real mail in production, visible mail locally |
| Enumeration | register, forgot password and resend answer the same way whether the email exists or not | no account discovery |
| Tokens | email confirmation 24 h, password reset 1 h, single use, ASP.NET Core data protection | standard |

## Work packages

1. **Store and model** (0.5 d): `StriveUser` (id, email, normalized email, display name, password hash, email confirmed,
   lockout fields, created at), unique index on the normalized email, the user store, data protection keys persisted in
   MongoDB so tokens survive restarts and multiple instances.
2. **Account service** (0.5 d): register, confirm email, sign in (password check, lockout, require confirmed email),
   forgot and reset password, resend confirmation. All as plain services with unit tests, no controller logic.
3. **Sign-in integration** (0.25 d): `AccountController` picks the demo provider or the account service from the mode,
   `ProfileService` returns display name and email-verified claims, subject id handling for both schemes.
4. **Email** (0.25 d): `IEmailSender`, MailKit sender, HTML and text templates in the app design, Mailpit in the dev
   compose file, `installation.md` section.
5. **Pages** (0.5 d): Register, Check your inbox, Confirm email, Forgot password, Reset password, links on the login
   page, validation messages, same design as the login page. Accessible (labels, focus, error summary).
6. **Hardening** (0.25 d): rate limiting on the account endpoints (per IP and per email), anti-forgery on every post,
   consistent response timing for unknown emails, security headers, no user details in logs.
7. **Tests** (0.5 d): unit tests for services and store (in-memory Mongo), integration tests with
   `WebApplicationFactory` for register, confirm, sign in, lockout, reset (reading the mail from a fake sender),
   a Cypress spec for the account flow reading mail from the Mailpit API, the demo specs stay unchanged.

Total: about 2.5 days.

## Out of scope (can follow)

- Social sign-in (Google, Microsoft), two-factor authentication, account deletion and data export, admin user
  management, profile picture.

## Risks and open questions

- Which email provider will production use? The plan only needs SMTP.
- Should registration be open to everyone or restricted to allowed email domains or invitations? Default: open.
- Existing users of a running demo deployment keep their demo identity; there is no way to attach an account to it.
  Acceptable because demo identities are not real accounts.
