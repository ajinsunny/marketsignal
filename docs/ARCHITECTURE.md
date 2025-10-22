# Signal Copilot – Lite: Architecture Documentation

## Table of Contents
1. [System Architecture](#system-architecture)
2. [Backend Architecture](#backend-architecture)
3. [Frontend Architecture](#frontend-architecture)
4. [Database Design](#database-design)
5. [Service Layer](#service-layer)
6. [Authentication & Security](#authentication--security)
7. [Background Jobs](#background-jobs)
8. [API Design](#api-design)
9. [Data Flow](#data-flow)
10. [Deployment Architecture](#deployment-architecture)

---

## System Architecture

### High-Level Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         User Interface                          │
│              (Next.js 15.5.5 + React 19.1.0)                   │
└─────────────────────────────────────────────────────────────────┘
                              │ HTTPS/REST
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    API Gateway / CORS                           │
│                  (ASP.NET Core Middleware)                      │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│ Controllers  │     │  Background  │     │   Hangfire   │
│   (REST)     │     │    Jobs      │     │  Dashboard   │
└──────────────┘     └──────────────┘     └──────────────┘
        │                     │                     │
        └─────────────────────┼─────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                       Service Layer                             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │ News     │  │Sentiment │  │ Impact   │  │ Alert    │       │
│  │Aggregator│  │ Analyzer │  │Calculator│  │ Service  │       │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘       │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │Portfolio │  │Historical│  │Consensus │  │Portfolio │       │
│  │ Analyzer │  │ Analogs  │  │Calculator│  │Analytics │       │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Data Access Layer                            │
│              (Entity Framework Core 8.0.11)                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   PostgreSQL Database                           │
│  (Users, Holdings, Articles, Signals, Impacts, Alerts)         │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   NewsAPI    │     │   Finnhub    │     │  SEC Edgar   │
│  (Optional)  │     │  (Optional)  │     │   (Free)     │
└──────────────┘     └──────────────┘     └──────────────┘
```

### Technology Stack

| Layer | Component | Technology | Version |
|-------|-----------|-----------|---------|
| **Frontend** | UI Framework | Next.js | 15.5.5 |
| | UI Library | React | 19.1.0 |
| | Language | TypeScript | 5.0 |
| | Styling | Tailwind CSS | 4.0 |
| | Build Tool | Turbopack | Built-in |
| **Backend** | Web Framework | ASP.NET Core | 8.0 |
| | Language | C# | 12.0 |
| | ORM | Entity Framework Core | 8.0.11 |
| | Authentication | ASP.NET Identity + JWT | Built-in |
| | API Documentation | Swagger/Swashbuckle | 6.6.2 |
| | Background Jobs | Hangfire | 1.8.18 |
| **Database** | Primary Database | PostgreSQL | Latest |
| | Provider | Npgsql | 8.0.11 |
| | Hangfire Storage | Hangfire.PostgreSql | 1.20.12 |
| **External** | News Source 1 | NewsAPI.org | REST API |
| | News Source 2 | Finnhub | REST API |
| | News Source 3 | SEC Edgar | REST API |
| | Email Delivery | SendGrid | REST API |
| | AI Vision | Claude API | Anthropic |

---

## Backend Architecture

### Clean Architecture Layers

```
┌──────────────────────────────────────────────────────────────┐
│                     Presentation Layer                       │
│                                                              │
│  ├─ Controllers/                                            │
│  │   ├─ AuthController.cs                                  │
│  │   ├─ HoldingsController.cs                              │
│  │   ├─ ImpactsController.cs                               │
│  │   ├─ PortfolioController.cs                             │
│  │   ├─ ProfileController.cs                               │
│  │   ├─ AnalysisController.cs                              │
│  │   ├─ JobsController.cs                                  │
│  │   └─ HealthController.cs                                │
│  │                                                          │
│  └─ Program.cs (Startup configuration)                     │
└──────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│                    Application Layer                         │
│                                                              │
│  ├─ Services/ (Business Logic)                             │
│  │   ├─ News Aggregation                                   │
│  │   │   ├─ NewsAggregationService.cs                     │
│  │   │   ├─ NewsApiProvider.cs                            │
│  │   │   ├─ FinnhubProvider.cs                            │
│  │   │   └─ SecEdgarService.cs                            │
│  │   │                                                     │
│  │   ├─ Analysis                                           │
│  │   │   ├─ SentimentAnalyzer.cs                          │
│  │   │   ├─ ImpactCalculator.cs                           │
│  │   │   ├─ ConsensusCalculator.cs                        │
│  │   │   └─ HistoricalAnalogService.cs                    │
│  │   │                                                     │
│  │   ├─ Portfolio Management                               │
│  │   │   ├─ PortfolioAnalyzer.cs                          │
│  │   │   ├─ PortfolioAnalytics.cs                         │
│  │   │   └─ ClaudeImageProcessor.cs                       │
│  │   │                                                     │
│  │   ├─ Alerting                                           │
│  │   │   └─ AlertService.cs                               │
│  │   │                                                     │
│  │   └─ Background                                         │
│  │       └─ BackgroundJobsService.cs                      │
│  │                                                         │
│  └─ Interfaces/ (Abstractions)                            │
│      ├─ INewsProvider.cs                                  │
│      ├─ ISentimentAnalyzer.cs                             │
│      ├─ IImpactCalculator.cs                              │
│      ├─ IPortfolioAnalyzer.cs                             │
│      ├─ IAlertService.cs                                  │
│      └─ IImageProcessor.cs                                │
└──────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│                     Domain Layer                             │
│                                                              │
│  ├─ Models/ (Entities)                                     │
│  │   ├─ ApplicationUser.cs                                │
│  │   ├─ Holding.cs                                        │
│  │   ├─ Article.cs                                        │
│  │   ├─ Signal.cs                                         │
│  │   ├─ Impact.cs                                         │
│  │   └─ Alert.cs                                          │
│  │                                                         │
│  └─ Enums/                                                 │
│      ├─ EventCategory.cs                                  │
│      ├─ SourceType.cs                                     │
│      ├─ RiskProfile.cs (in ApplicationUser.cs)            │
│      ├─ HoldingIntent.cs (in Holding.cs)                  │
│      └─ AlertType.cs, AlertStatus.cs (in Alert.cs)        │
└──────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────┐
│                 Infrastructure Layer                         │
│                                                              │
│  ├─ Data/                                                  │
│  │   └─ ApplicationDbContext.cs                           │
│  │                                                         │
│  └─ Migrations/                                            │
│      ├─ 20251015215442_InitialCreate.cs                   │
│      ├─ 20251016111753_EnhanceSignalQuality.cs            │
│      └─ 20251016113126_EnhancePersonalization.cs          │
└──────────────────────────────────────────────────────────────┘
```

### Dependency Injection

Configured in `Program.cs`:

```csharp
// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Application Services
builder.Services.AddScoped<INewsProvider, NewsApiProvider>();
builder.Services.AddScoped<INewsProvider, FinnhubProvider>();
builder.Services.AddScoped<NewsAggregationService>();
builder.Services.AddScoped<ISentimentAnalyzer, SentimentAnalyzer>();
builder.Services.AddScoped<IImpactCalculator, ImpactCalculator>();
builder.Services.AddScoped<IPortfolioAnalyzer, PortfolioAnalyzer>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<BackgroundJobsService>();

// Hangfire
builder.Services.AddHangfire(config => config
    .UsePostgreSqlStorage(connectionString));
```

---

## Frontend Architecture

### Next.js App Router Structure

```
frontend/
├── app/
│   ├── layout.tsx                 # Root layout
│   ├── page.tsx                   # Landing page (/)
│   ├── globals.css                # Global styles
│   └── dashboard/
│       └── page.tsx               # Dashboard (/dashboard)
│
├── components/
│   ├── ProfileSetup.tsx           # User preferences modal
│   ├── PortfolioContext.tsx       # Portfolio analytics display
│   └── EvidencePill.tsx           # Impact evidence visualization
│
├── lib/
│   └── api.ts                     # Type-safe API client
│
├── public/                        # Static assets
├── next.config.ts                 # Next.js configuration
├── tailwind.config.ts             # Tailwind configuration
├── tsconfig.json                  # TypeScript configuration
└── package.json                   # Dependencies
```

### Component Hierarchy

```
RootLayout
├── Landing Page (/)
│   ├── Hero Section
│   ├── Feature Showcase
│   │   ├── News Ingestion Card
│   │   ├── Impact Scoring Card
│   │   └── Smart Alerts Card
│   ├── Impact Formula Display
│   └── "Jump In" CTA
│
└── Dashboard (/dashboard)
    ├── Header
    ├── Main Content (Grid Layout)
    │   ├── Left Column
    │   │   ├── Add Holding Form
    │   │   ├── Portfolio Image Upload
    │   │   └── Holdings Table
    │   │       ├── Edit/Delete Actions
    │   │       └── Intent Display
    │   │
    │   └── Right Column
    │       ├── Impact Feed
    │       │   ├── Impact Card
    │       │   │   ├── Headline
    │       │   │   ├── Impact Score Badge
    │       │   │   ├── Evidence Pill
    │       │   │   └── Article Details
    │       │   └── Pagination
    │       │
    │       └── Job Controls
    │
    ├── Sticky Sidebar
    │   ├── Profile Setup Button
    │   ├── Portfolio Context
    │   │   ├── Total Value
    │   │   ├── Concentration Index
    │   │   ├── Intent Breakdown
    │   │   └── Top Positions
    │   │
    │   └── Rebalance Suggestions
    │       └── Recommendation Cards
    │
    └── Modals
        └── Profile Setup
            ├── Risk Profile Selector
            └── Cash Buffer Input
```

### State Management

Uses React 19 hooks and server state synchronization:

```typescript
// Component-level state
const [holdings, setHoldings] = useState<Holding[]>([]);
const [impacts, setImpacts] = useState<Impact[]>([]);
const [profile, setProfile] = useState<UserProfile | null>(null);

// API state synchronization
useEffect(() => {
  loadHoldings();
  loadImpacts();
  loadProfile();
}, []);

// Optimistic updates
const handleAddHolding = async (holding: Holding) => {
  setHoldings([...holdings, holding]); // Optimistic
  try {
    await api.addHolding(holding);
  } catch (error) {
    setHoldings(holdings); // Rollback on error
  }
};
```

### API Client Architecture

Type-safe client with centralized error handling:

```typescript
// lib/api.ts
class ApiClient {
  private baseUrl: string;
  private token: string | null;

  async get<T>(endpoint: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      headers: this.getHeaders(),
    });
    return this.handleResponse<T>(response);
  }

  async post<T>(endpoint: string, data: unknown): Promise<T> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify(data),
    });
    return this.handleResponse<T>(response);
  }

  private async handleResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
      throw new Error(`API error: ${response.statusText}`);
    }
    return response.json();
  }
}
```

---

## Database Design

### Entity Relationship Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                      ApplicationUser                            │
│  (ASP.NET Identity + Custom Fields)                            │
├─────────────────────────────────────────────────────────────────┤
│ PK  Id (string)                                                 │
│     Email (string)                                              │
│     UserName (string)                                           │
│     PasswordHash (string)                                       │
│     Timezone (string) = "UTC"                                   │
│     CreatedAt (datetime)                                        │
│     LastLoginAt (datetime, nullable)                            │
│     RiskProfile (enum: Conservative|Balanced|Aggressive)        │
│     CashBuffer (decimal(18,2), nullable)                        │
└─────────────────────────────────────────────────────────────────┘
        │ 1                                              │ 1
        │                                                │
        ├─────────────┐                                 │
        │             │                                  │
        │ N           │ N                                │ N
        ▼             ▼                                  ▼
┌──────────┐  ┌──────────┐                      ┌──────────┐
│ Holding  │  │  Impact  │                      │  Alert   │
├──────────┤  ├──────────┤                      ├──────────┤
│PK Id     │  │PK Id     │                      │PK Id     │
│FK UserId │  │FK UserId │                      │FK UserId │
│  Ticker  │  │FK ArticleId                    │  Type    │
│  Shares  │  │FK HoldingId                    │  Status  │
│  Cost... │  │  ImpactScore                   │  Content │
│  Intent  │  │  Exposure│                      │  Sent... │
└──────────┘  │  Computed│                      └──────────┘
     │ 1      └──────────┘
     │              │ N        N │
     │              └─────┬──────┘
     │                    │
     │ N                  │ 1
     │                    ▼
     │           ┌────────────────┐
     │           │    Article     │
     │           ├────────────────┤
     │           │PK Id           │
     │           │  Ticker        │
     │           │  Headline      │
     │           │  SourceUrl     │
     │           │  Publisher     │
     │           │  PublishedAt   │
     │           │  SourceType    │
     │           │  SourceTier    │
     │           │  EventCategory │
     │           │  ClusterId     │
     │           └────────────────┘
     │                    │ 1
     │                    │
     └────────────────────┼───────────┐
                          │           │
                          │ 1         │ N
                          ▼           ▼
                   ┌────────────┐  ┌──────────┐
                   │   Signal   │  │  Impact  │
                   ├────────────┤  └──────────┘
                   │PK Id       │
                   │FK ArticleId│ (UNIQUE)
                   │  Sentiment │
                   │  Magnitude │
                   │  Confidence│
                   │  SourceCount│
                   │  Stance... │
                   │  Consensus │
                   └────────────┘
```

### Table Schemas

#### ApplicationUser
```sql
CREATE TABLE "AspNetUsers" (
    "Id" TEXT PRIMARY KEY,
    "Email" TEXT NOT NULL,
    "UserName" TEXT NOT NULL,
    "PasswordHash" TEXT,
    "Timezone" TEXT DEFAULT 'UTC',
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "LastLoginAt" TIMESTAMP,
    "RiskProfile" INTEGER NOT NULL DEFAULT 1, -- 0=Conservative, 1=Balanced, 2=Aggressive
    "CashBuffer" NUMERIC(18,2)
);
```

#### Holdings
```sql
CREATE TABLE "Holdings" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Ticker" VARCHAR(10) NOT NULL,
    "Shares" NUMERIC(18,8) NOT NULL,
    "CostBasis" NUMERIC(18,2),
    "AcquiredAt" TIMESTAMP,
    "Intent" INTEGER NOT NULL DEFAULT 3, -- 0=Trade, 1=Accumulate, 2=Income, 3=Hold
    "AddedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP,

    CONSTRAINT "FK_Holdings_Users" FOREIGN KEY ("UserId")
        REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    CONSTRAINT "UQ_Holdings_UserTicker" UNIQUE ("UserId", "Ticker")
);

CREATE INDEX "IX_Holdings_UserId" ON "Holdings"("UserId");
CREATE INDEX "IX_Holdings_Ticker" ON "Holdings"("Ticker");
```

#### Articles
```sql
CREATE TABLE "Articles" (
    "Id" SERIAL PRIMARY KEY,
    "Ticker" VARCHAR(10) NOT NULL,
    "Headline" VARCHAR(500) NOT NULL,
    "Summary" VARCHAR(2000),
    "SourceUrl" VARCHAR(1000),
    "Publisher" VARCHAR(200),
    "PublishedAt" TIMESTAMP NOT NULL,
    "IngestedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "SourceType" INTEGER NOT NULL DEFAULT 0, -- 0=Unknown, 1=SecFiling, 2=PressRelease...
    "SourceTier" INTEGER NOT NULL DEFAULT 0, -- 0=Unknown, 1=Premium, 2=Standard...
    "EventCategory" INTEGER NOT NULL DEFAULT 0,
    "ClusterId" VARCHAR(50),
    "RelatedTickers" VARCHAR(500)
);

CREATE INDEX "IX_Articles_Ticker" ON "Articles"("Ticker");
CREATE INDEX "IX_Articles_PublishedAt" ON "Articles"("PublishedAt" DESC);
CREATE INDEX "IX_Articles_ClusterId" ON "Articles"("ClusterId") WHERE "ClusterId" IS NOT NULL;
```

#### Signals
```sql
CREATE TABLE "Signals" (
    "Id" SERIAL PRIMARY KEY,
    "ArticleId" INTEGER NOT NULL,
    "Sentiment" INTEGER NOT NULL CHECK ("Sentiment" IN (-1, 0, 1)),
    "Magnitude" INTEGER NOT NULL CHECK ("Magnitude" BETWEEN 1 AND 3),
    "Confidence" NUMERIC(5,2) NOT NULL CHECK ("Confidence" BETWEEN 0 AND 1),
    "Reasoning" VARCHAR(1000),
    "AnalyzedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "EventCategory" INTEGER NOT NULL DEFAULT 0,
    "SourceCount" INTEGER NOT NULL DEFAULT 1 CHECK ("SourceCount" BETWEEN 1 AND 100),
    "StanceAgreement" NUMERIC(5,2) NOT NULL DEFAULT 1.0 CHECK ("StanceAgreement" BETWEEN 0 AND 1),
    "ConsensusFactor" NUMERIC(5,2) NOT NULL DEFAULT 1.0 CHECK ("ConsensusFactor" BETWEEN 0 AND 1),

    CONSTRAINT "FK_Signals_Articles" FOREIGN KEY ("ArticleId")
        REFERENCES "Articles"("Id") ON DELETE CASCADE,
    CONSTRAINT "UQ_Signals_ArticleId" UNIQUE ("ArticleId")
);
```

#### Impacts
```sql
CREATE TABLE "Impacts" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "ArticleId" INTEGER NOT NULL,
    "HoldingId" INTEGER NOT NULL,
    "ImpactScore" NUMERIC(10,4) NOT NULL,
    "Exposure" NUMERIC(5,4) NOT NULL CHECK ("Exposure" BETWEEN 0 AND 1),
    "ComputedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "FK_Impacts_Users" FOREIGN KEY ("UserId")
        REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Impacts_Articles" FOREIGN KEY ("ArticleId")
        REFERENCES "Articles"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Impacts_Holdings" FOREIGN KEY ("HoldingId")
        REFERENCES "Holdings"("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Impacts_UserId_ArticleId" ON "Impacts"("UserId", "ArticleId");
CREATE INDEX "IX_Impacts_ImpactScore" ON "Impacts"("ImpactScore" DESC);
CREATE INDEX "IX_Impacts_ComputedAt" ON "Impacts"("ComputedAt" DESC);
```

#### Alerts
```sql
CREATE TABLE "Alerts" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Type" INTEGER NOT NULL, -- 0=DailyDigest, 1=HighImpact
    "Status" INTEGER NOT NULL DEFAULT 0, -- 0=Pending, 1=Sent, 2=Failed
    "Subject" VARCHAR(200),
    "Content" TEXT NOT NULL,
    "ArticleIds" VARCHAR(1000),
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "SentAt" TIMESTAMP,
    "ErrorMessage" VARCHAR(500),

    CONSTRAINT "FK_Alerts_Users" FOREIGN KEY ("UserId")
        REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Alerts_UserId_CreatedAt" ON "Alerts"("UserId", "CreatedAt" DESC);
CREATE INDEX "IX_Alerts_Status" ON "Alerts"("Status") WHERE "Status" = 0; -- Pending alerts
```

### Decimal Precision Strategy

| Field | Precision | Rationale |
|-------|-----------|-----------|
| **Shares** | (18, 8) | Supports fractional shares (0.00000001 precision) |
| **CostBasis** | (18, 2) | Currency precision (cents) |
| **Confidence** | (5, 2) | Percentage (0.00 to 1.00) |
| **ImpactScore** | (10, 4) | High precision for ranking (-9999.9999 to 9999.9999) |
| **Exposure** | (5, 4) | Portfolio weight (0.0001 to 1.0000) |
| **CashBuffer** | (18, 2) | Currency precision |

---

## Service Layer

### Core Services

#### 1. NewsAggregationService
**Purpose:** Orchestrate multi-source news fetching

```csharp
public class NewsAggregationService {
    private readonly IEnumerable<INewsProvider> _providers;

    public async Task<List<Article>> FetchNewsForTickers(List<string> tickers) {
        var allArticles = new List<Article>();

        // Fetch from all enabled providers in parallel
        var tasks = _providers.Select(p => p.FetchNews(tickers));
        var results = await Task.WhenAll(tasks);

        foreach (var articles in results) {
            allArticles.AddRange(articles);
        }

        // Deduplicate by ClusterId
        return DeduplicateArticles(allArticles);
    }
}
```

#### 2. SentimentAnalyzer
**Purpose:** Analyze article sentiment with finance-aware keywords

```csharp
public class SentimentAnalyzer : ISentimentAnalyzer {
    public async Task<Signal> AnalyzeArticle(Article article) {
        var text = $"{article.Headline} {article.Summary}".ToLower();

        // Calculate sentiment (-1, 0, 1)
        var sentiment = CalculateSentiment(text);

        // Estimate magnitude (1-3) based on event category
        var magnitude = EstimateMagnitude(article.EventCategory, text);

        // Calculate confidence (0.0-1.0)
        var confidence = CalculateConfidence(article, text);

        return new Signal {
            ArticleId = article.Id,
            Sentiment = sentiment,
            Magnitude = magnitude,
            Confidence = confidence,
            AnalyzedAt = DateTime.UtcNow
        };
    }

    private int CalculateSentiment(string text) {
        var positiveCount = CountKeywords(text, _positiveKeywords);
        var negativeCount = CountKeywords(text, _negativeKeywords);

        if (positiveCount > negativeCount) return 1;
        if (negativeCount > positiveCount) return -1;
        return 0;
    }
}
```

#### 3. ImpactCalculator
**Purpose:** Calculate personalized impact scores

```csharp
public class ImpactCalculator : IImpactCalculator {
    public async Task<List<Impact>> CalculateImpacts(Article article, Signal signal) {
        var impacts = new List<Impact>();

        // Get all users holding this ticker
        var holdings = await _context.Holdings
            .Where(h => h.Ticker == article.Ticker)
            .Include(h => h.User)
            .ToListAsync();

        foreach (var holding in holdings) {
            // Calculate portfolio value for this user
            var portfolioValue = await CalculatePortfolioValue(holding.UserId);

            // Calculate exposure (0.0-1.0)
            var positionValue = holding.Shares * holding.CostBasis ?? 0;
            var exposure = portfolioValue > 0
                ? (decimal)(positionValue / portfolioValue)
                : 0;

            // Apply concentration multiplier
            var concentrationMultiplier = exposure > 0.15m ? 1.2m : 1.0m;

            // Calculate impact score
            var impactScore = signal.Sentiment
                * signal.Magnitude
                * signal.Confidence
                * exposure
                * concentrationMultiplier;

            impacts.Add(new Impact {
                UserId = holding.UserId,
                ArticleId = article.Id,
                HoldingId = holding.Id,
                ImpactScore = impactScore,
                Exposure = exposure,
                ComputedAt = DateTime.UtcNow
            });
        }

        return impacts;
    }
}
```

#### 4. PortfolioAnalyzer
**Purpose:** Generate personalized recommendations

```csharp
public class PortfolioAnalyzer : IPortfolioAnalyzer {
    public async Task<AnalysisResult> GenerateRecommendations(string userId) {
        // Get user's recent impacts (7-day window)
        var impacts = await GetRecentImpacts(userId, days: 7);

        // Group by ticker
        var tickerGroups = impacts.GroupBy(i => i.Article.Ticker);

        var recommendations = new List<RebalanceRecommendation>();

        foreach (var group in tickerGroups) {
            var ticker = group.Key;
            var holding = await GetHolding(userId, ticker);

            // Get historical analogs
            var analogs = await _analogService.FindSimilarEvents(ticker);

            // Calculate aggregate impact
            var totalImpact = group.Sum(i => i.ImpactScore);

            // Generate recommendation
            var recommendation = await GenerateRecommendation(
                holding,
                totalImpact,
                analogs
            );

            recommendations.Add(recommendation);
        }

        return new AnalysisResult {
            Summary = GenerateSummary(recommendations),
            Recommendations = recommendations
        };
    }
}
```

---

## Authentication & Security

### JWT Authentication Flow

```
1. User Registration
   ├─ POST /api/auth/register
   ├─ Password validation (8+ chars, digit, upper, lower)
   ├─ Hash password (BCrypt via Identity)
   └─ Create ApplicationUser

2. User Login
   ├─ POST /api/auth/login
   ├─ Validate credentials
   ├─ Generate JWT token
   │   ├─ Claims: Sub (UserId), Email, Jti
   │   ├─ Expiration: 1440 minutes (24 hours)
   │   └─ Signature: HMAC-SHA256
   └─ Return token

3. Authenticated Requests
   ├─ Client includes: Authorization: Bearer <token>
   ├─ Middleware validates signature
   ├─ Extract User.Identity
   └─ Authorize access to resources
```

### Security Measures

#### 1. Password Policy
```csharp
builder.Services.Configure<IdentityOptions>(options => {
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
});
```

#### 2. CORS Configuration
```csharp
// Development: Allow any origin
if (app.Environment.IsDevelopment()) {
    app.UseCors(policy => policy
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
}

// Production: Whitelist specific origins
else {
    app.UseCors(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowCredentials()
        .AllowAnyMethod()
        .AllowAnyHeader());
}
```

#### 3. Hangfire Dashboard Authorization
```csharp
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter {
    public bool Authorize(DashboardContext context) {
        var httpContext = context.GetHttpContext();

        // Development: Allow all
        if (isDevelopment) return true;

        // Production: Require JWT or dashboard key
        var authHeader = httpContext.Request.Headers["Authorization"];
        var dashboardKey = httpContext.Request.Headers["X-Hangfire-Dashboard-Key"];

        return ValidateJwt(authHeader) || ValidateDashboardKey(dashboardKey);
    }
}
```

#### 4. Secret Management
- Secrets stored in `appsettings.json` (development)
- Environment variables in production
- `.gitignore` excludes all config files
- `.env.example` templates for deployment

---

## Background Jobs

### Hangfire Configuration

```csharp
// Startup
builder.Services.AddHangfire(config => config
    .UsePostgreSqlStorage(connectionString)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings());

builder.Services.AddHangfireServer();

// Middleware
app.UseHangfireDashboard("/hangfire", new DashboardOptions {
    Authorization = new[] { new HangfireAuthorizationFilter() }
});
```

### Recurring Jobs

```csharp
public class BackgroundJobsService {
    public void ConfigureRecurringJobs() {
        // News fetch: Every 30 minutes
        RecurringJob.AddOrUpdate(
            "fetch-news",
            () => FetchNewsForAllTickers(),
            "*/30 * * * *" // Cron expression
        );

        // Daily digest: 9 AM daily
        RecurringJob.AddOrUpdate(
            "daily-digest",
            () => GenerateDailyDigests(),
            "0 9 * * *"
        );

        // High-impact alerts: Every hour
        RecurringJob.AddOrUpdate(
            "high-impact-alerts",
            () => GenerateHighImpactAlerts(),
            "0 * * * *"
        );
    }
}
```

### Job Orchestration

```
News Fetch Job
├─ Get all unique tickers from Holdings
├─ Fetch articles from all enabled providers
├─ For each new article:
│   ├─ Analyze sentiment → create Signal
│   └─ Calculate impacts → create Impacts
└─ Save to database

Daily Digest Job
├─ Get all users
├─ For each user:
│   ├─ Get impacts from last 24 hours
│   ├─ Generate digest content
│   └─ Create Alert (type: DailyDigest)
└─ (Future: Send via SendGrid)

High-Impact Alert Job
├─ Get impacts above threshold (0.7) from last hour
├─ Group by user
├─ For each user with high-impact events:
│   ├─ Generate alert content
│   └─ Create Alert (type: HighImpact)
└─ (Future: Send via SendGrid)
```

---

## API Design

### RESTful Conventions

| HTTP Method | Endpoint Pattern | Purpose |
|-------------|------------------|---------|
| GET | `/api/{resource}` | List all |
| GET | `/api/{resource}/{id}` | Get by ID |
| POST | `/api/{resource}` | Create new |
| PUT | `/api/{resource}/{id}` | Update existing |
| DELETE | `/api/{resource}/{id}` | Delete |

### Response Format

#### Success (200 OK)
```json
{
  "data": { ... }
}
```

#### Created (201 Created)
```json
{
  "id": 123,
  "message": "Holding created successfully"
}
```

#### Error (400/401/404/500)
```json
{
  "error": "Invalid ticker symbol",
  "details": "Ticker must be 1-10 uppercase characters"
}
```

### Pagination

```json
{
  "impacts": [ ... ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 156,
    "totalPages": 8
  }
}
```

---

## Data Flow

### Impact Scoring Pipeline

```
1. News Ingestion
   ├─ Background job fetches articles
   ├─ Multi-source aggregation
   ├─ Deduplication by ClusterId
   └─ Save to Articles table

2. Sentiment Analysis
   ├─ For each new article:
   ├─ Keyword-based sentiment detection
   ├─ Event category classification
   ├─ Confidence calculation
   │   ├─ Source quality multiplier
   │   ├─ Rumor detection
   │   └─ Consensus factor
   └─ Save to Signals table (1:1 with Article)

3. Impact Calculation
   ├─ Find all users holding this ticker
   ├─ For each user:
   │   ├─ Calculate portfolio value
   │   ├─ Calculate exposure (position / portfolio)
   │   ├─ Apply concentration multiplier (>15%)
   │   └─ Compute impact score
   └─ Save to Impacts table

4. Alert Generation
   ├─ Query impacts above threshold
   ├─ Group by user
   ├─ Generate alert content
   └─ Save to Alerts table (status: Pending)

5. Alert Delivery (Future)
   ├─ Query pending alerts
   ├─ Send via SendGrid
   └─ Update status (Sent/Failed)
```

---

## Deployment Architecture

### Multi-Platform Support

#### 1. AWS App Runner + RDS
```
Internet → CloudFront → App Runner → RDS PostgreSQL
                          │
                          └─→ SendGrid (email)
                          └─→ NewsAPI (news)
```

#### 2. Railway
```
Internet → Railway (Full Stack)
           ├─ Web Service (ASP.NET Core)
           ├─ PostgreSQL (Managed)
           └─ Cron Jobs (Hangfire)
```

#### 3. Vercel + Separate Backend
```
Frontend: Internet → Vercel (Next.js)
Backend:  Internet → Railway/Heroku/AWS (ASP.NET Core)
Database: RDS/Railway/Supabase (PostgreSQL)
```

### Infrastructure as Code (AWS CDK)

```typescript
// infrastructure/lib/infrastructure-stack.ts
export class InfrastructureStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    // RDS PostgreSQL
    const db = new rds.DatabaseInstance(this, 'Database', {
      engine: rds.DatabaseInstanceEngine.postgres({ version: rds.PostgresEngineVersion.VER_15 }),
      instanceType: ec2.InstanceType.of(ec2.InstanceClass.T3, ec2.InstanceSize.MICRO),
      vpc,
    });

    // App Runner
    const service = new apprunner.Service(this, 'Service', {
      source: apprunner.Source.fromEcrPublic({
        imageConfiguration: { port: 8080 },
        imageIdentifier: 'public.ecr.aws/...',
      }),
    });
  }
}
```

---

## Performance Considerations

### Database Optimization
- Indexes on frequently queried columns (UserId, Ticker, ImpactScore)
- Composite indexes for common joins (UserId + ArticleId)
- Decimal precision optimized for storage

### Caching Strategy (Future)
- Redis for frequent queries (portfolio metrics)
- In-memory cache for user profiles
- HTTP cache headers for static content

### Async Processing
- Background jobs for heavy operations
- Parallel news fetching
- Async/await throughout

---

## Monitoring & Observability

### Health Checks
- `/api/health`: Basic liveness check
- `/api/health/detailed`: Database connectivity, service status

### Logging
- ASP.NET Core logging
- Hangfire job history
- Error tracking (future: Sentry)

### Metrics (Future)
- User activity
- API latency
- Job success rates
- Alert delivery rates

---

**Last Updated:** October 21, 2025
**Version:** 1.0
