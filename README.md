# ByteBill_BS

ByteBill_BS is an ASP.NET Core MVC business management system for service-oriented operations and billing. It includes role-based area modules, invoicing and payment flows, inventory and service catalog management, audit logging, real-time notifications, and third-party integrations for accounting, email, and online payments.

> Important: this repository currently contains API keys and client secrets in appsettings files. Rotate them immediately and move secrets to user-secrets or environment variables.

## Highlights

- Multi-area architecture for SuperAdmin, Admin, Billing, Technician, and Auditor roles.
- Cookie-based authentication with role claims, access policies, rate-limited login, and lockout tracking.
- End-to-end billing workflow with invoice, payment, adjustment, and tax computation services.
- SQL Server data layer using Entity Framework Core 9.
- Real-time notifications via SignalR.
- PDF generation using QuestPDF.
- Integrations for PayMongo (payments), Xero (accounting), and SendGrid (email).

## Tech Stack

- ASP.NET Core MVC (.NET 9)
- Entity Framework Core 9 (SQL Server)
- SignalR
- QuestPDF
- SendGrid SDK
- BCrypt.Net

## Project Structure

- Areas/
	- Admin/
	- Auditor/
	- Billing/
	- SuperAdmin/
	- Technician/
- Controllers/
	- MVC and API controllers for auth, profile, archive, notifications, and domain operations
- Data/
	- ApplicationDbContext and startup seeding
- Models/
	- Domain entities and enums
- Services/
	- Business logic layer (billing, tax, notifications, integrations, and more)
- DTOs/ and ViewModels/
	- API and UI transfer models
- Hubs/
	- SignalR hubs
- Migrations/
	- EF Core migrations
- Database/
	- SQL migration and deployment scripts
- wwwroot/
	- Static assets

## Requirements

- .NET SDK 9.0+
- SQL Server LocalDB (for local development) or SQL Server instance
- Optional external accounts for:
	- SendGrid
	- Xero
	- PayMongo

## Getting Started

1. Clone and open the solution.

2. Restore dependencies.

```powershell
dotnet restore
```

3. Configure app settings.

- Update ConnectionStrings:DefaultConnection to your local SQL Server target.
- Configure SendGrid, Xero, and PayMongo values as needed.
- Prefer environment variables or user secrets for sensitive values.
- Use appsettings.Example.json as the baseline template.

4. Apply database migrations.

```powershell
dotnet ef database update
```

If EF tools are not installed:

```powershell
dotnet tool install --global dotnet-ef
```

5. Run the application.

```powershell
dotnet run
```

6. Open the local URL shown in terminal output.

## Configuration Template

- Baseline template file: appsettings.Example.json
- Keep appsettings.json/appsettings.Development.json for local defaults only.
- Put all real keys in user-secrets or environment variables.

Example override using environment variables (PowerShell):

```powershell
$env:SendGrid__ApiKey = "<your-sendgrid-key>"
$env:PayMongo__SecretKey = "<your-paymongo-secret>"
$env:Xero__ClientSecret = "<your-xero-secret>"
```

## First-Run Checklist

1. Ensure LocalDB exists:

```powershell
sqllocaldb info
```

2. If needed, create/start LocalDB:

```powershell
sqllocaldb create MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

3. Run the app once to trigger startup seeding.

4. Sign in with a seeded demo user (below).

## Seeded Demo Accounts

These users are created on an empty database by the seeder.

| Role | Username | Password |
|------|----------|----------|
| SuperAdmin | vkpadao | Superadmin123! |
| Admin | admin | Admin123! |
| Billing | billing | Billing123! |
| Technician | technician | Technician123! |
| Auditor | auditor | Auditor123! |

Change demo passwords immediately in non-dev environments.

## Runtime Behavior

- The app sets default culture to en-PH.
- QuestPDF runs under Community license mode.
- On startup, LocalDB start is attempted via sqllocaldb start MSSQLLocalDB.
- Database seeding is attempted on startup; seeding errors are logged and do not crash the app.
- In development, HTTPS redirection is enabled.
- SignalR hub endpoint: /hubs/notifications

## Module Access Map

- SuperAdmin area: platform-wide management, integrations, and cross-shop governance.
- Admin area: shop-level operations and management.
- Billing area: invoices, payments, adjustments, and billing workflows.
- Technician area: service execution, diagnostics, and job order updates.
- Auditor area: read-focused visibility for review and compliance.

## Authentication and Authorization

- Cookie authentication with login path /Auth/Login and access denied path /Auth/AccessDenied.
- Login endpoint is rate-limited (fixed window).
- In-memory lockout logic after repeated failed login attempts.
- Role-based authorization policies:
	- AdminOrAbove
	- BillingOrAbove
	- TechnicianOrAbove
	- AnyAuthenticated

## Database Notes

- EF migrations are maintained under Migrations/.
- Additional SQL scripts for data/schema adjustments are available in Database/.
- Local development defaults to ByteBillDB in LocalDB.

## Configuration Keys

Common sections in appsettings:

- ConnectionStrings
- PayMongo
- Xero
- SendGrid
- Logging

Recommended secret handling:

- Use dotnet user-secrets locally.
- Use environment variables or secure secret stores in production.
- Do not commit live credentials.

### Suggested Local Secret Commands

```powershell
dotnet user-secrets init
dotnet user-secrets set "SendGrid:ApiKey" "<your-sendgrid-key>"
dotnet user-secrets set "PayMongo:SecretKey" "<your-paymongo-secret>"
dotnet user-secrets set "Xero:ClientSecret" "<your-xero-secret>"
```

You can keep non-sensitive defaults in appsettings and override only secrets with user-secrets.

## Useful Commands

Build:

```powershell
dotnet build
```

Run:

```powershell
dotnet run
```

Create migration:

```powershell
dotnet ef migrations add <MigrationName>
```

Update database:

```powershell
dotnet ef database update
```

## API Endpoints (Quick Reference)

Base API routes are under /api.

CustomersApiController:
- GET /api/CustomersApi
- GET /api/CustomersApi/{id}
- POST /api/CustomersApi
- PUT /api/CustomersApi/{id}

InvoicesApiController:
- GET /api/InvoicesApi
- GET /api/InvoicesApi/metrics
- GET /api/InvoicesApi/{id}
- POST /api/InvoicesApi
- POST /api/InvoicesApi/{id}/adjustments

JobOrdersApiController:
- GET /api/JobOrdersApi
- GET /api/JobOrdersApi/{id}
- POST /api/JobOrdersApi
- PUT /api/JobOrdersApi/{id}/assign
- POST /api/JobOrdersApi/{id}/services
- DELETE /api/JobOrdersApi/{id}/services/{lineId}
- POST /api/JobOrdersApi/{id}/parts
- DELETE /api/JobOrdersApi/{id}/parts/{lineId}

NotificationsApiController:
- GET /api/notifications
- GET /api/notifications/paged
- POST /api/notifications/read/{id}
- POST /api/notifications/read-all

PaymentsApiController:
- GET /api/PaymentsApi
- GET /api/PaymentsApi/metrics
- GET /api/PaymentsApi/{id}
- POST /api/PaymentsApi

PayMongoApiController:
- POST /api/PayMongoApi/link
- POST /api/PayMongoApi/checkout
- GET /api/PayMongoApi/status/{txnId}
- GET /api/PayMongoApi/invoice/{invoiceId}
- POST /api/PayMongoApi/webhook

PaymentCallbackController:
- GET /payment/success
- GET /payment/cancel

## Deployment Notes

- web.config is included for IIS hosting scenarios.
- Verify forwarded headers and HTTPS behavior in your hosting environment.
- Ensure production-grade values for cookies, secrets, and callback URLs.

## Troubleshooting

- Error: Cannot connect to LocalDB.
	- Verify LocalDB instance is created and running via sqllocaldb info and sqllocaldb start MSSQLLocalDB.
- Error: EF migration update fails.
	- Check DefaultConnection in appsettings and confirm SQL Server/LocalDB is reachable.
- App starts but login fails for demo users.
	- Confirm database is empty on first run or reset DB and rerun seeding.
- Payment/accounting callbacks fail locally.
	- Ensure callback URLs in PayMongo/Xero settings match the active local URL and scheme.
- SignalR notifications not appearing.
	- Confirm hub endpoint /hubs/notifications is reachable and browser session is authenticated.

## Development Notes

- Startup currently uses the developer exception page globally.
- Seed failures are logged but do not terminate app startup.
- Login path is rate-limited and lockout logic is tracked in memory.

## Suggested Next Improvements

- Move all secrets out of appsettings files.
- Add a dedicated environment sample file for onboarding.
- Add automated test coverage for core billing and authentication flows.
- Add CI checks for build, migration verification, and basic smoke tests.