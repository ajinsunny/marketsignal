# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Signal Copilot – Lite** is an ASP.NET Core 8.0 Web API that converts financial news into personalized impact alerts for investment portfolios. It filters market noise by computing impact scores based on sentiment, magnitude, confidence, and user exposure to specific holdings.

### Core Concept
The system calculates an **Impact Score** for each news article per user:
```
Impact Score = direction × magnitude × confidence × exposure
```

Only events with |impact| ≥ configured threshold trigger alerts (daily digest or high-impact notifications).

## Development Commands

### Build & Run
```bash
# Build solution
dotnet build

# Run API (from src/SignalCopilot.Api/)
dotnet run
# Access at: https://localhost:5001
# Swagger UI: https://localhost:5001/swagger
# Hangfire Dashboard: https://localhost:5001/hangfire

# Restore packages
dotnet restore
```

### Database Migrations
```bash
cd src/SignalCopilot.Api

# Create new migration
dotnet ef migrations add <MigrationName>

# Apply migrations to database
dotnet ef database update

# Remove last migration (if not applied)
dotnet ef migrations remove

# Generate SQL script
dotnet ef migrations script
```

### Testing & Debugging
```bash
# Run from solution root or API directory
dotnet test

# Watch mode for development
dotnet watch run
```

## Architecture

### Data Flow Pipeline
```
User → Portfolio API → News Ingestor → Analyzer → Scorer → Notifier → UI/Email
```

1. **Portfolio Management**: Users add holdings (ticker + shares + cost basis)
2. **News Ingestion**: Background jobs fetch headlines for tracked tickers (NewsAPI.org)
3. **Analysis**: Sentiment scoring engine evaluates articles
4. **Impact Computation**: Per-user scores calculated based on exposure
5. **Alerting**: High-impact events trigger immediate alerts; daily digest summarizes activity

### Core Domain Models & Relationships

**ApplicationUser** (extends ASP.NET Identity)
- Has many: Holdings, Impacts, Alerts
- Properties: Timezone, CreatedAt, LastLoginAt

**Holding** → **Impact** ← **Article**
- Holding: User's position (UserId + Ticker + Shares + CostBasis)
- Article: News item (Ticker + Headline + SourceUrl + Publisher)
- Signal: Analysis result (ArticleId + Sentiment [-1,0,1] + Magnitude [1-3] + Confidence [0-1])
- Impact: Personalized score (UserId + ArticleId + HoldingId + ImpactScore + Exposure)

**Key Relationship**: One Article has one Signal. Each Article generates multiple Impacts (one per affected user).

**Alert**: Notifications (UserId + Type [DailyDigest, HighImpact] + Status + Content + ArticleIds)

### Database Configuration (ApplicationDbContext.cs)

Critical indexes:
- `Holdings`: Unique constraint on (UserId, Ticker)
- `Signals`: Unique constraint on ArticleId (one-to-one with Article)
- `Impacts`: Composite index on (UserId, ArticleId), index on ImpactScore for threshold queries
- `Alerts`: Composite index on (UserId, CreatedAt), index on Status

Decimal precision:
- Shares: (18, 8) - supports fractional shares
- CostBasis: (18, 2) - currency precision
- Confidence: (5, 2) - percentage (0.00-1.00)
- ImpactScore: (10, 4) - high precision for ranking
- Exposure: (5, 4) - portfolio weight (0.0000-1.0000)

### Authentication & Authorization

- **ASP.NET Identity**: User management with ApplicationUser
- **JWT Bearer Tokens**: Stateless auth with configurable expiration
- All controllers except AuthController require `[Authorize]` attribute
- Current user ID retrieved via: `User.FindFirst(ClaimTypes.NameIdentifier)?.Value`

### Background Job System (Hangfire)

Configured with PostgreSQL storage. Dashboard accessible at `/hangfire` (development mode allows all access).

Intended jobs (not yet implemented):
- News ingestion (scheduled, e.g., every 30 minutes)
- Impact computation (after news ingestion)
- Daily digest generation (time-based from AlertSettings)
- Alert delivery (email via SendGrid)

### Configuration Structure (appsettings.json)

Required settings:
- `ConnectionStrings:DefaultConnection` - PostgreSQL connection
- `JwtSettings` - SecretKey (≥32 chars), Issuer, Audience, ExpirationMinutes
- `NewsApi` - ApiKey, BaseUrl
- `SendGrid` - ApiKey, FromEmail, FromName
- `AlertSettings` - HighImpactThreshold (decimal), DailyDigestTime (HH:mm)

## API Controllers

### AuthController (`/api/auth`)
- `POST /register` - Create user account
- `POST /login` - Authenticate and receive JWT token

### HoldingsController (`/api/holdings`) [Authorized]
- `GET /` - List user's holdings
- `GET /{id}` - Get single holding
- `POST /` - Add new holding (prevents duplicates by ticker)
- `PUT /{id}` - Update shares/cost basis
- `DELETE /{id}` - Remove holding

### ImpactsController (`/api/impacts`) [Authorized]
- `GET /` - Paginated impacts with optional minImpactScore filter
- `GET /high-impact` - Top 10 impacts above threshold, includes Signal details

## Future Implementation Areas

1. **News Ingestion Service**: Create service to fetch from NewsAPI.org for tracked tickers
2. **Sentiment Analyzer**: Implement keyword-based or ML sentiment scoring
3. **Impact Calculator**: Service to compute scores when new articles/signals arrive
4. **Alert Generator**: Create daily digests and high-impact notifications
5. **Email Service**: SendGrid integration for alert delivery
6. **Feedback System**: User tagging (useful/noise) for model improvement
7. **Hangfire Authorization**: Replace HangfireAuthorizationFilter with proper auth in production

## Project Constraints & Philosophy

- **Awareness tool, not financial advice** - clearly label in all user-facing content
- **Filter unverified/rumor headlines** - validate source credibility
- **Minimal personal data** - store only necessary portfolio information
- **Out of scope**: Trading execution, social media scraping, paid research feeds, alpha predictions

## Tech Stack

- ASP.NET Core 8.0 Web API
- Entity Framework Core 8.0.11 with PostgreSQL (Npgsql)
- ASP.NET Identity + JWT Bearer Authentication
- Hangfire 1.8.18 for background jobs
- Swagger/Swashbuckle for API documentation
- SendGrid (planned) for email notifications
- Please update the plan to make sure we build a frontend for the project. I think using Next.js for frontend as per README is a good choice. Please don't create a Register/Login for user. Just have the user jump into the application by clicking a button called "Jump In".