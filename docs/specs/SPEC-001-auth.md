# SPEC-001: Authentication

- **Status:** Draft
- **Milestone:** M1 MVP
- **Issues:** #9 (API), #10 (UI)
- **Related:** session-strategy ADR (pending, written in #9)

## Goal
As a visitor, I can create an account and sign in, because Dictora requires an account before it can be used.

## User flow
1. A visitor opens any page and is redirected to **Login** if they aren't signed in.
2. From Login they can go to **Register**, create an account with email + password, and are signed in right after.
3. After signing in, they're returned to the page they originally asked for (or Home).
4. They can **Log out** from the header at any time.

## Rules and edge cases
- Email is unique and case-insensitive.
- Passwords are stored hashed (ASP.NET Core Identity or equivalent), never in plain text.
- Every API endpoint except register/login requires authentication by default.
- The frontend (Vercel) and API (Railway) are on different domains. The session strategy (httpOnly cookie vs JWT) must work across them and is recorded in an ADR.
- A wrong email or password shows one generic error (don't reveal which one was wrong).
- All UI text is in English and Vietnamese.

## Acceptance criteria

### API (#9)
- [ ] `POST /auth/register`, `POST /auth/login`, `POST /auth/logout`, `GET /me`
- [ ] Passwords hashed; duplicate email rejected with a clear error code
- [ ] Session strategy decided and recorded as an ADR in `docs/decisions/`
- [ ] All other endpoints require auth by default
- [ ] Tests: register + login happy path, wrong password, duplicate email

### UI (#10)
- [ ] Register and Login pages with validation errors shown
- [ ] Protected routes redirect to Login; after login, the user returns to the original page
- [ ] Logout in the header
- [ ] All text in `en` + `vi`

## Out of scope
- Social login (Google, etc.)
- Password reset, email verification
- Roles / admin
