# Signal Copilot – Lite: Project Overview

## Executive Summary

**Signal Copilot – Lite** is a personalized financial news filtering system that converts market noise into actionable impact alerts. Built with ASP.NET Core 8.0 and Next.js 15, it helps investors focus on news that truly matters to their specific portfolio holdings.

**Status:** Active Development (Phase 4B - Portfolio Analytics)
**Started:** October 15, 2025
**Technology Stack:** ASP.NET Core 8.0, Next.js 15.5.5, PostgreSQL, Hangfire

## Core Problem & Solution

### The Problem
Investors are overwhelmed by thousands of daily headlines but lack tools to filter what's actually relevant to their specific holdings. Generic news feeds don't account for:
- Personal portfolio composition
- Position sizes and exposure
- Individual risk tolerance
- Holding timeframes and investment intent

### The Solution
Signal Copilot analyzes every news article and calculates a personalized **Impact Score** for each user:

```
Impact Score = Sentiment × Magnitude × Confidence × Exposure × ConcentrationMultiplier
```

Where:
- **Sentiment**: -1 (negative), 0 (neutral), 1 (positive)
- **Magnitude**: 1-3 based on event severity
- **Confidence**: 0.0-1.0 from source quality and consensus
- **Exposure**: 0.0-1.0 representing portfolio weight
- **ConcentrationMultiplier**: 1.2x for positions over 15%

This formula ensures users only see news that matters to their specific situation.

## Key Features

### 1. Portfolio Management
- Add/edit/delete holdings with ticker, shares, cost basis
- Track holding intent (Trade, Accumulate, Income, Hold)
- Portfolio image upload with AI ticker extraction
- Fractional share support (up to 8 decimal places)

### 2. News Aggregation
- Multi-source ingestion (NewsAPI, Finnhub, SEC Edgar)
- Automatic deduplication via clustering
- Source quality tiers (Premium, Standard, Social, Official)
- Event categorization (13 predefined categories)

### 3. Sentiment Analysis
- Finance-aware keyword dictionaries
- Rumor detection and confidence adjustment
- Magnitude scoring (1=minor, 2=moderate, 3=major)
- Multi-source consensus calculation

### 4. Impact Scoring
- Personalized per-user calculations
- Concentration risk weighting
- Exposure-based relevance
- Historical analog patterns

### 5. Portfolio Analytics
- Concentration index (Herfindahl-Hirschman Index)
- Intent-based allocation breakdown
- Top position analysis
- Risk profile integration

### 6. Smart Alerts
- Daily digests (configurable time)
- High-impact notifications (threshold-based)
- Email delivery via SendGrid
- Alert status tracking

## Architecture Overview

### High-Level Data Flow
```
User → Portfolio API → News Ingestor → Analyzer → Scorer → Notifier → UI/Email
```

### Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| **Backend** | ASP.NET Core | 8.0 |
| **Frontend** | Next.js + React | 15.5.5 / 19.1.0 |
| **Database** | PostgreSQL | via Npgsql |
| **ORM** | Entity Framework Core | 8.0.11 |
| **Auth** | ASP.NET Identity + JWT | Built-in |
| **Jobs** | Hangfire | 1.8.18 |
| **Styling** | Tailwind CSS | 4.0 |
| **Language** | TypeScript / C# | 5.0 / 12.0 |

### Core Domain Models

```
ApplicationUser
├── Holdings (1:N)
├── Impacts (1:N)
└── Alerts (1:N)

Holding
└── Impacts (1:N)

Article
├── Signal (1:1)
└── Impacts (1:N)

Signal
└── Article (1:1)

Impact
├── User (N:1)
├── Article (N:1)
└── Holding (N:1)
```

## Project Philosophy

### Core Principles
1. **Awareness Tool, Not Financial Advice** - Clearly labeled, provides insights not recommendations
2. **Filter Unverified Headlines** - Rumor detection, source validation, consensus checking
3. **Minimal Personal Data** - Only essential portfolio information
4. **Privacy First** - No social data, no behavior tracking beyond usage

### Out of Scope (v1)
- Trading execution
- Social media scraping
- Paid research feeds
- Alpha predictions
- Broker API integration

## Current State (Phase 4B)

### Completed Features ✅
- Full portfolio CRUD operations
- Multi-source news aggregation
- Sentiment analysis with confidence scoring
- Impact calculation with concentration weighting
- Portfolio analytics dashboard
- User profile management (risk tolerance, cash buffer)
- Historical analog pattern matching
- Intent-based tracking
- Portfolio context visualization

### In Progress 🚧
- Background job automation
- Email alert delivery
- Multi-source consensus refinement

### Planned Features 📋
- Sector-specific analysis packs
- Alternative data signals
- Community verification system
- Read-only broker sync (Plaid/Alpaca)

## Quick Start

### Backend
```bash
cd src/SignalCopilot.Api
dotnet restore
dotnet ef database update
dotnet run
# API: https://localhost:5001
# Swagger: https://localhost:5001/swagger
# Hangfire: https://localhost:5001/hangfire
```

### Frontend
```bash
cd frontend
npm install
npm run dev
# UI: http://localhost:3000
```

### Environment Variables
Required configuration in `appsettings.json`:
- PostgreSQL connection string
- JWT secret key (≥32 chars)
- News API keys (optional, SEC Edgar is free)
- SendGrid API key for email alerts

## API Endpoints

| Controller | Endpoints | Purpose |
|-----------|-----------|---------|
| **Auth** | `/api/auth/register`, `/api/auth/login` | User authentication |
| **Holdings** | `/api/holdings/*` | Portfolio management |
| **Impacts** | `/api/impacts/*` | Impact feed & high-impact events |
| **Portfolio** | `/api/portfolio/*` | Analytics & image upload |
| **Profile** | `/api/profile` | User preferences |
| **Analysis** | `/api/analysis/rebalance-suggestions` | Recommendations |
| **Jobs** | `/api/jobs/*` | Manual job triggers |
| **Health** | `/api/health` | System status |

## Project Structure

```
marketsignal/
├── src/
│   ├── SignalCopilot.Api/          # ASP.NET Core backend
│   │   ├── Controllers/             # 8 REST controllers
│   │   ├── Services/                # 22+ service classes
│   │   ├── Models/                  # Domain entities
│   │   ├── Data/                    # DbContext
│   │   └── Migrations/              # EF migrations
│   └── SignalCopilot.Api.Tests/    # Unit tests
├── frontend/                        # Next.js frontend
│   ├── app/                         # Pages & routing
│   ├── components/                  # React components
│   └── lib/                         # API client
├── infrastructure/                  # AWS CDK deployment
├── scripts/                         # Deployment scripts
├── docs/                           # Documentation
├── README.md                       # Project specifications
└── CLAUDE.md                       # Development guidelines
```

## Development Roadmap

### Phase 1: MVP (Completed)
- Portfolio CRUD
- News ingestion (NewsAPI)
- Keyword-based sentiment analysis
- Impact calculation
- Alert generation

### Phase 2: Signal Quality (Completed)
- EventCategory taxonomy
- SourceType and SourceTier
- Multi-source consensus
- Confidence scoring refinements

### Phase 3: Personalization (Completed)
- Risk profiles (Conservative, Balanced, Aggressive)
- Cash buffer tracking
- User profile management
- Intent-based holdings

### Phase 4A: Historical Evidence (Completed)
- Similar event lookup
- Median price move analysis
- Pattern-based recommendations

### Phase 4B: Portfolio Analytics (Current)
- Concentration index (HHI)
- Intent-based allocation
- Holding performance tracking
- Portfolio context visualization

### Phase 5: Automation & Polish (Next)
- Background job refinement
- Email alert delivery
- Performance optimization
- UI/UX enhancements

### Future Enhancements
- Sector-specific packs
- Alternative data signals
- Community verification
- Broker sync (read-only)

## Contributing

This project uses:
- C# 12.0 / .NET 8.0 for backend
- TypeScript 5.0 / React 19.1.0 for frontend
- Entity Framework Core for data access
- Hangfire for background jobs
- Tailwind CSS for styling

See `CLAUDE.md` for detailed development guidelines and architecture decisions.

## License

Proprietary - All rights reserved

## Contact

For questions or support, please refer to project documentation or contact the development team.

---

**Last Updated:** October 21, 2025
**Version:** 0.4.0 (Phase 4B)
