# Part 2: RaceDay API

ASP.NET Core 8 Web API with Entity Framework Core (code first) on SQL Server, session login, two roles, Swagger, and xUnit tests.

## Run it

You need the .NET 8 SDK and a SQL Server. Docker is the quickest way to get one:

```
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong!Passw0rd" -p 1433:1433 --name racedaysql -d mcr.microsoft.com/mssql/server:2022-latest
```

If you used a different password, change it in `Part2-API/RaceDay.Api/appsettings.json` under `ConnectionStrings`.

Then:

```
cd Part2-API
dotnet restore
dotnet run --project RaceDay.Api
```

Open http://localhost:5080/swagger. The API creates a database called `RaceDayApiDB` on first start and fills it with the same sample data as the Part 1 SQL script.

### Make the first migration

```
dotnet tool install --global dotnet-ef
cd Part2-API/RaceDay.Api
dotnet ef migrations add InitialCreate
dotnet ef database drop
dotnet ef database update
```

Commit the new `Migrations` folder. After that the API applies migrations on start.

## Try it in Swagger

Sample accounts (password for all of them is `Password123!`):

| Role | Email |
|---|---|
| Organiser | thabo.nkosi@raceday.co.za |
| Organiser | lindiwe.dube@raceday.co.za |
| Participant | sipho.mokoena@example.com |
| Participant | amahle.zulu@example.com |

1. Call `POST /api/auth/login` with one of them.
2. Call any other endpoint. Swagger sends the session cookie for you.
3. Call `POST /api/auth/logout` and try again to see the 401.

## The two roles

**Organiser** creates, edits and deletes events, manages categories, sees every enrolment for their own events, confirms enrolments and captures results.

**Participant** browses events, enrols under a category, sees and cancels their own enrolments, and reads their own results.

Registering as an Organiser needs an invite code (`Auth:OrganiserInviteCode` in `appsettings.json`, currently `RACEDAY-ORGANISER-2026`). Anyone can register as a Participant. This stops a visitor from simply choosing the Organiser role.

## How login and roles work

- `POST /api/auth/login` checks the password and stores the user id and role in a server-side session. The browser only holds a session cookie.
- `[SessionAuthorize]` guards each endpoint. No session gives 401. The wrong role gives 403.
- Organisers can only change their own events, categories and results (403 otherwise).
- Passwords are hashed with PBKDF2 and a random salt. The plain password is never stored.

## Tests

```
cd Part2-API
dotnet test
```

The tests start the real API in memory with an in-memory database, so no SQL Server is needed. They cover registration and login, event management by Organisers, wrong-role rejection in both directions, enrolments being recorded, and results.

## CI/CD

`.github/workflows/part2-api-ci.yml` builds the solution and runs every test on each push.

![Part 2 green build](part2-ci.png)

## Video

_Unlisted YouTube link goes here._

## AI disclosure

_Write what you actually used AI for here._
