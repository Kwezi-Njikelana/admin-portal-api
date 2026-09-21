# Admin Portal API

A REST API for managing employees and departments, built with ASP.NET Core and PostgreSQL.

## Tech stack

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core with Npgsql (PostgreSQL)
- Swagger / OpenAPI (available in Development)
- Built-in rate limiting and health checks

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (running locally or accessible via connection string)

## Getting started

1. **Configure the database connection**

   Update the `ConnectionStrings:DefaultConnection` value in `appsettings.json` (or `appsettings.Development.json`, or [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)) to point at your PostgreSQL instance:

   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Port=5432;Database=EmployeeAdminPortal;Username=postgres;Password=yourpassword"
   }
   ```

2. **Apply database migrations**

   ```bash
   dotnet ef database update
   ```

3. **Run the API**

   ```bash
   dotnet run
   ```

   In development, Swagger UI is available at `/swagger` for exploring and testing endpoints.

## Configuration

| Setting | Description | Default |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string | — |
| `RateLimiting:PermitLimit` | Max requests per client IP per window | `100` |
| `RateLimiting:WindowSeconds` | Rate limit window length, in seconds | `60` |

A `/health` endpoint reports database connectivity status.

## API overview

All endpoints are prefixed with `/api`.

### Employees

| Method | Route | Description |
|---|---|---|
| GET | `/api/employees` | List employees. Supports `search`, `departmentId`, `page`, `pageSize` query params |
| GET | `/api/employees/{id}` | Get an employee by id |
| POST | `/api/employees` | Create an employee |
| PUT | `/api/employees/{id}` | Update an employee |
| DELETE | `/api/employees/{id}` | Delete an employee |

### Departments

| Method | Route | Description |
|---|---|---|
| GET | `/api/departments` | List all departments |
| GET | `/api/departments/{id}` | Get a department by id |
| GET | `/api/departments/{id}/employees` | List employees in a department. Supports `page`, `pageSize` |
| POST | `/api/departments` | Create a department |
| PUT | `/api/departments/{id}` | Update a department |
| DELETE | `/api/departments/{id}` | Delete a department (fails if employees are still assigned) |

Errors are returned as [RFC 7807 ProblemDetails](https://datatracker.ietf.org/doc/html/rfc7807) (`application/problem+json`).

## Project structure

```
Controllers/    API endpoints
Models/         Request DTOs and response wrappers
Models/Entities Employee and Department entities
Data/           EF Core DbContext
Migrations/     EF Core database migrations
Middleware/     Global exception handling
```
