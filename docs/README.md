# Signal Copilot Documentation

Welcome to the Signal Copilot – Lite documentation. This directory contains comprehensive guides for understanding, developing, and deploying the project.

## Documentation Overview

### Core Documentation

1. **[PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md)**
   - Executive summary and project vision
   - Core problem and solution
   - Key features and capabilities
   - Technology stack
   - Development roadmap
   - Quick start guide
   - **Start here** if you're new to the project

2. **[ARCHITECTURE.md](ARCHITECTURE.md)**
   - System architecture diagrams
   - Backend and frontend architecture
   - Database design and ERD
   - Service layer documentation
   - Authentication and security
   - Background jobs (Hangfire)
   - Data flow pipelines
   - Deployment architecture
   - **Read this** to understand how everything works

3. **[API_REFERENCE.md](API_REFERENCE.md)**
   - Complete REST API documentation
   - All endpoints with examples
   - Request/response formats
   - Error handling
   - Authentication guide
   - SDK examples (TypeScript, cURL)
   - **Use this** when integrating with the API

4. **[COMMIT_HISTORY.md](COMMIT_HISTORY.md)**
   - Detailed analysis of all commits
   - Development timeline
   - Feature evolution
   - Technical decisions
   - Lessons learned
   - **Read this** to understand project evolution

### Deployment Documentation

5. **[QUICK_DEPLOY.md](QUICK_DEPLOY.md)** ⚡ NEW!
   - Fast-track production deployment (1-2 hours)
   - Step-by-step instructions
   - Neon (database) + Render (backend) + Vercel (frontend)
   - Free tier setup ($12/year for domain only)
   - **Start here** for quickest path to production

6. **[PRODUCTION_DEPLOYMENT.md](PRODUCTION_DEPLOYMENT.md)** 📦 NEW!
   - Comprehensive deployment guide
   - Multiple platform options (Render/Railway/Fly.io)
   - Environment variable configuration
   - DNS setup and SSL certificates
   - Security hardening
   - Monitoring and observability
   - Cost estimation and scaling
   - Troubleshooting guide
   - **Read this** for complete production setup

7. **[DEPLOYMENT_CHECKLIST.md](DEPLOYMENT_CHECKLIST.md)** ✅ NEW!
   - Pre-deployment tasks
   - Environment variable checklist
   - Step-by-step deployment phases
   - Post-deployment verification
   - Monitoring setup
   - Rollback procedures
   - Security audit
   - **Use this** to ensure nothing is missed

## Quick Links

### For Deployment
1. **New to deployment?** → [QUICK_DEPLOY.md](QUICK_DEPLOY.md) - Get online in 1-2 hours
2. **Need comprehensive guide?** → [PRODUCTION_DEPLOYMENT.md](PRODUCTION_DEPLOYMENT.md)
3. **Deploying now?** → [DEPLOYMENT_CHECKLIST.md](DEPLOYMENT_CHECKLIST.md) - Follow the checklist

### For New Developers
1. Read [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) - Get the big picture
2. Review [ARCHITECTURE.md](ARCHITECTURE.md) - Understand the system
3. Check [API_REFERENCE.md](API_REFERENCE.md) - Learn the API
4. See `../README.md` - Project specifications
5. See `../CLAUDE.md` - Development guidelines

### For API Consumers
1. [API_REFERENCE.md](API_REFERENCE.md) - Complete API docs
2. [Authentication](#authentication) - How to authenticate
3. [SDK Examples](#sdk-examples) - Code samples

### For DevOps/Deployment
1. [QUICK_DEPLOY.md](QUICK_DEPLOY.md) - **Start here** for fastest deployment (1-2 hours)
2. [PRODUCTION_DEPLOYMENT.md](PRODUCTION_DEPLOYMENT.md) - Comprehensive deployment guide
3. [DEPLOYMENT_CHECKLIST.md](DEPLOYMENT_CHECKLIST.md) - Pre/post deployment checklist
4. [ARCHITECTURE.md#deployment-architecture](ARCHITECTURE.md#deployment-architecture) - Infrastructure overview
5. `../AWS_DEPLOYMENT.md` - AWS-specific deployment (advanced)
6. `../scripts/README.md` - Deployment automation scripts

## Documentation Standards

### File Organization
```
docs/
├── README.md                    # This file (documentation index)
├── PROJECT_OVERVIEW.md          # High-level project summary
├── ARCHITECTURE.md              # Technical architecture
├── API_REFERENCE.md             # API documentation
├── COMMIT_HISTORY.md            # Development history
├── QUICK_DEPLOY.md              # ⚡ Quick deployment guide (1-2 hours)
├── PRODUCTION_DEPLOYMENT.md     # 📦 Comprehensive deployment guide
├── DEPLOYMENT_CHECKLIST.md      # ✅ Deployment task checklist
└── [Future additions]
    ├── DEVELOPER_GUIDE.md       # Development best practices
    ├── TROUBLESHOOTING.md       # Common issues and solutions
    └── CHANGELOG.md             # Version history
```

### Markdown Conventions
- Use headings hierarchically (H1 → H2 → H3)
- Include table of contents for long documents
- Use code blocks with language syntax highlighting
- Include diagrams where helpful (ASCII art or Mermaid)
- Add "Last Updated" date at bottom of each doc

### Code Examples
- Provide working, tested examples
- Include both request and response
- Show error cases, not just happy path
- Use realistic data (no "foo", "bar")

## Getting Started

### Prerequisites
Before diving into the documentation, ensure you have:
- Basic understanding of ASP.NET Core and Next.js
- Familiarity with PostgreSQL
- Understanding of REST APIs and JWT authentication
- Docker knowledge (for deployment)

### Development Workflow
1. **Setup**
   - Clone repository
   - Install dependencies (dotnet restore, npm install)
   - Configure database (connection string)
   - Run migrations (dotnet ef database update)

2. **Development**
   - Start backend: `cd src/SignalCopilot.Api && dotnet run`
   - Start frontend: `cd frontend && npm run dev`
   - Access Swagger: `https://localhost:5001/swagger`
   - Access UI: `http://localhost:3000`

3. **Testing**
   - Run tests: `dotnet test`
   - Manual API testing via Swagger
   - Frontend testing in browser

4. **Deployment**
   - Build Docker image: `docker build -f src/SignalCopilot.Api/Dockerfile .`
   - Deploy to chosen platform (AWS, Railway, Vercel)
   - Configure environment variables
   - Run health checks

## Key Concepts

### Impact Score Formula
The heart of Signal Copilot is the Impact Score calculation:

```
Impact Score = Sentiment × Magnitude × Confidence × Exposure × ConcentrationMultiplier

Where:
- Sentiment: -1 (negative), 0 (neutral), 1 (positive)
- Magnitude: 1-3 (event severity)
- Confidence: 0.0-1.0 (source quality + consensus)
- Exposure: 0.0-1.0 (position weight in portfolio)
- ConcentrationMultiplier: 1.2 if exposure > 15%, else 1.0
```

### Data Flow
```
News Sources → Aggregation → Sentiment Analysis → Impact Calculation → Alerts
     ↓              ↓              ↓                    ↓              ↓
  NewsAPI      Deduplication   Keyword-based      Per-user       Daily Digest
  Finnhub      Clustering      Finance-aware      Personalized   High-Impact
  SEC Edgar    Source Tiers    Confidence         Exposure       Email
```

### Personalization Layers
1. **Portfolio Composition**: What you own determines what news you see
2. **Position Size**: Larger positions get higher impact scores
3. **Risk Profile**: Conservative, Balanced, or Aggressive framing
4. **Holding Intent**: Trade, Accumulate, Income, or Hold strategies
5. **Historical Context**: Similar past events inform recommendations

## Architecture Highlights

### Backend (ASP.NET Core 8.0)
- Clean architecture with service layer
- Entity Framework Core for data access
- Hangfire for background jobs
- JWT authentication
- Swagger/OpenAPI documentation

### Frontend (Next.js 15.5.5)
- React 19.1.0 with TypeScript
- App Router for modern routing
- Tailwind CSS 4.0 for styling
- Type-safe API client
- Server and client components

### Database (PostgreSQL)
- 7 core entities (User, Holding, Article, Signal, Impact, Alert)
- Strategic indexes for performance
- Optimized decimal precision
- Foreign key constraints

### Background Jobs (Hangfire)
- News fetch every 30 minutes
- Daily digest at 9 AM
- High-impact alerts hourly
- PostgreSQL-backed job storage

## API Overview

### Authentication Endpoints
- `POST /api/auth/register` - Create account
- `POST /api/auth/login` - Get JWT token

### Portfolio Management
- `GET /api/holdings` - List holdings
- `POST /api/holdings` - Add holding
- `PUT /api/holdings/{id}` - Update holding
- `DELETE /api/holdings/{id}` - Remove holding

### Impact & Analysis
- `GET /api/impacts` - Paginated impact feed
- `GET /api/impacts/high-impact` - Top 10 impacts
- `GET /api/analysis/rebalance-suggestions` - Recommendations

### Portfolio Analytics
- `GET /api/portfolio/metrics` - Concentration index
- `GET /api/portfolio/intent-metrics` - Intent breakdown
- `POST /api/portfolio/upload-image` - Extract tickers from screenshot

### User Preferences
- `GET /api/profile` - Get risk profile and cash buffer
- `PUT /api/profile` - Update preferences

## Common Tasks

### Adding a New Feature
1. Update domain model (Models/)
2. Create migration (`dotnet ef migrations add <Name>`)
3. Implement service logic (Services/)
4. Add controller endpoint (Controllers/)
5. Update API client (frontend/lib/api.ts)
6. Build UI component (frontend/components/)
7. Update documentation

### Deploying to Production
1. Set environment variables
2. Update CORS allowed origins
3. Configure database connection
4. Set JWT secret key
5. Add SendGrid API key
6. Run migrations
7. Build and deploy
8. Verify health checks

### Debugging Issues
1. Check API logs
2. Verify database connectivity
3. Inspect Hangfire dashboard
4. Review Swagger documentation
5. Test with cURL or Postman
6. Check CORS configuration
7. Validate JWT token

## Performance Considerations

### Database Optimization
- Indexes on UserId, Ticker, ImpactScore
- Composite indexes for joins
- Decimal precision for storage efficiency
- Connection pooling

### Caching (Future)
- Redis for portfolio metrics
- In-memory user profiles
- HTTP cache headers

### Async Processing
- Background jobs for heavy operations
- Parallel news fetching
- Async/await throughout

## Security Best Practices

### Authentication
- Secure password hashing (ASP.NET Identity)
- JWT tokens with expiration
- HTTPS only in production

### Data Protection
- No credit card or SSN storage
- Minimal personal data
- Portfolio data encrypted at rest

### API Security
- CORS whitelist in production
- Rate limiting (planned)
- Input validation on all endpoints
- SQL injection prevention (EF Core parameterization)

## Monitoring & Observability

### Health Checks
- `/api/health` - Basic liveness
- `/api/health/detailed` - Comprehensive status

### Logging
- ASP.NET Core structured logging
- Hangfire job history
- Error tracking (future: Sentry)

### Metrics (Planned)
- User activity
- API latency (p50, p95, p99)
- Job success rates
- Alert delivery rates

## Contributing

### Development Guidelines
See `../CLAUDE.md` for detailed guidelines on:
- Build commands
- Database migrations
- Testing procedures
- Code style
- Commit message format

### Documentation Updates
When making changes:
1. Update relevant markdown files
2. Keep code examples current
3. Update API reference if endpoints change
4. Add entries to CHANGELOG.md
5. Update "Last Updated" dates

## Support & Resources

### Internal Resources
- **README.md**: Project specifications
- **CLAUDE.md**: Development guidelines
- **Swagger UI**: Interactive API testing
- **Hangfire Dashboard**: Job monitoring

### External Resources
- [ASP.NET Core Docs](https://docs.microsoft.com/en-us/aspnet/core/)
- [Next.js Docs](https://nextjs.org/docs)
- [PostgreSQL Docs](https://www.postgresql.org/docs/)
- [Hangfire Docs](https://docs.hangfire.io/)

### Getting Help
1. Check existing documentation
2. Review Swagger API docs
3. Inspect Hangfire dashboard
4. Review commit history for context
5. Contact development team

## Roadmap

### Documentation Planned
- [ ] **DEPLOYMENT_GUIDE.md**: Step-by-step deployment
- [ ] **DEVELOPER_GUIDE.md**: Onboarding for new developers
- [ ] **TROUBLESHOOTING.md**: Common issues and solutions
- [ ] **CHANGELOG.md**: Version history
- [ ] **CONTRIBUTING.md**: Contribution guidelines
- [ ] **TESTING_GUIDE.md**: Testing strategies
- [ ] **PERFORMANCE_GUIDE.md**: Optimization techniques

### Documentation Improvements
- [ ] Add Mermaid diagrams for flows
- [ ] Video tutorials for key features
- [ ] Postman collection export
- [ ] OpenAPI 3.0 spec export
- [ ] Interactive examples
- [ ] Translation to other languages (future)

## License

Proprietary - All rights reserved

---

**Documentation Version:** 1.0
**Last Updated:** October 21, 2025
**Project Version:** 0.4.0 (Phase 4B)

For the latest updates, check the Git commit history.
