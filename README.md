# Phemelo Martin Mashome · Portfolio

My personal portfolio site, built with **ASP.NET Core 10 MVC** and **PostgreSQL**.

It shows my projects, experience, skills and certifications, has a working contact form, and includes a private admin area where I manage my certificates, profile photo and messages.

## Tech stack

| Layer | Technology |
|---|---|
| Web | ASP.NET Core 10 MVC, Razor views, plain CSS and JavaScript (no front-end framework) |
| Data | PostgreSQL with Entity Framework Core 10 (code-first migrations) |
| Auth | ASP.NET Core Identity with authenticator-app two-factor sign-in |
| Logging | Serilog (structured, with a request id on every log line) |
| Images | SkiaSharp for safe re-encoding of uploaded photos |

## Why PostgreSQL

The data is relational (certificates, messages, admin accounts and the keys that protect cookies) and needs real transactions. PostgreSQL has a free managed tier (Neon) with encrypted connections. SQLite was ruled out because containers lose their disk on every deploy.

## Security

- **Strict security headers:** Content-Security-Policy that only allows the site's own scripts, styles, fonts and images; no framing; no MIME sniffing. HSTS in production.
- **Forms:** anti-forgery tokens on every form, server-side validation of all input, and a hidden spam trap on the contact form.
- **Rate limiting:** the contact form allows 5 messages per 15 minutes per visitor, and sign-in allows 10 attempts per 15 minutes.
- **Admin sign-in:** password plus a TOTP authenticator code. The account locks for 15 minutes after 5 failed attempts. Recovery codes are shown once. There is no public registration.
- **Uploads:**
  - The real file type is checked from its first bytes.
  - Size limits apply.
  - Photos are re-encoded, which strips GPS and camera metadata.
  - PDFs containing scripts or embedded files are rejected.
- **Secrets:** never stored in the repository. Locally they live in `dotnet user-secrets`; in production they are environment variables.
- **Privacy (POPIA):** the contact form stores only name, email and message. Messages are deleted automatically after 180 days. No analytics, tracking or third-party requests (fonts are self-hosted).

## Error handling

- **Unexpected errors:** visitors see a friendly page with a reference code, while the full exception is logged under the same request id. No stack traces are shown in production.
- **Status pages:** custom 404, 429 (too many attempts) and generic error pages.
- **If the database is unreachable:** public pages keep working with built-in content and the original photo, and the contact form shows the email address instead of failing.
- **Health checks:** `/health` (is the app up?) and `/health/ready` (can it reach the database?).

## Running it locally

Requirements: .NET 10 SDK and PostgreSQL.

```powershell
# 1. Point the app at your local PostgreSQL (the database is created automatically)
dotnet user-secrets set "ConnectionStrings:Portfolio" "Host=localhost;Database=portfolio_dev;Username=postgres;Password=<your password>" --project src/Portfolio.Web

# 2. Create the admin account (12+ characters, with upper and lower case, a digit and a symbol)
dotnet user-secrets set "Admin:Email" "<your email>" --project src/Portfolio.Web
dotnet user-secrets set "Admin:Password" "<a strong password>" --project src/Portfolio.Web

# 3. Run it
dotnet run --project src/Portfolio.Web
```

Then open http://localhost:5206. The admin area is at `/admin`, and your first sign-in walks you through setting up the authenticator app.

## Deploying

The site runs on Render as a Docker container, with the database on Neon (managed PostgreSQL).

1. Create a Neon project in the Frankfurt region and copy its connection string.
2. On Render, choose **New → Blueprint** and select this repository. Render reads `render.yaml` and asks for:
   - `ConnectionStrings__Portfolio`: the Neon connection string
   - `Admin__Email` and `Admin__Password`: the admin account to create on first start
3. Once it's live, sign in at `/admin`, set up the authenticator app, then delete `Admin__Password` from the Render environment settings (it's only needed once).

Every push to `main` redeploys the site.

## Project structure

```
src/Portfolio.Web/
  Content/portfolio.json   Site text: profile, projects and case studies, skills, education
  Controllers/             Public pages, contact form, media, sitemap, error pages
  Areas/Admin/             Sign-in with 2FA, dashboard, messages, certificates, photo
  Data/                    EF Core context, entities, seed data, migrations
  Services/                Certificates, profile photo, upload checks, message retention
  Infrastructure/          Security headers, configuration helpers
  Views/                   Razor views
  wwwroot/                 CSS, JavaScript, self-hosted fonts, images, certificate PDFs
```

Most text changes (a new project, skill or job) only need an edit to `Content/portfolio.json`. The app checks that file when it starts and refuses to run if it's invalid, so mistakes are caught immediately.
