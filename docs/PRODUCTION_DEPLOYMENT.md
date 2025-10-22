# Signal Copilot – Production Deployment Guide

Complete guide for deploying Signal Copilot to production using Vercel, Render/Railway/Fly.io, and Neon/Supabase.

## Infrastructure Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         Users / Browsers                        │
└─────────────────────────────────────────────────────────────────┘
                              │
                    ┌─────────┴─────────┐
                    │                   │
                    ▼                   ▼
        ┌─────────────────┐   ┌─────────────────┐
        │     Vercel      │   │  Render/Railway │
        │   Next.js 15    │   │   ASP.NET API   │
        │ app.domain.com  │   │ api.domain.com  │
        └─────────────────┘   └─────────────────┘
                    │                   │
                    │                   │
                    │         ┌─────────┼──────────┐
                    │         │         │          │
                    ▼         ▼         ▼          ▼
            ┌──────────┐  ┌─────────┐ ┌────────┐ ┌──────────┐
            │   API    │  │  Neon   │ │Hangfire│ │ SendGrid │
            │          │  │Postgres │ │ Worker │ │  Email   │
            └──────────┘  └─────────┘ └────────┘ └──────────┘
```

### Platform Selection Rationale

| Service | Platform | Why |
|---------|----------|-----|
| **Frontend** | Vercel | Best Next.js 15 support, SSR/ISR, Edge, CDN, auto-preview |
| **Backend API** | Render/Railway/Fly | Docker support, always-on, affordable ($7-15/mo) |
| **Database** | Neon/Supabase | Managed Postgres, autoscaling, branching, free tier |
| **Background Jobs** | Hangfire (in backend) | Same container as API, shares DB, no extra cost |
| **Email** | SendGrid | 100 emails/day free, reliable delivery |
| **Object Storage** | Cloudflare R2/Supabase | Optional for future portfolio images |

## Pre-Deployment Checklist

### 1. Code Preparation
- [ ] All sensitive data moved to environment variables
- [ ] Production `appsettings.Production.json` configured
- [ ] CORS configured for production domains
- [ ] Hangfire dashboard secured with authentication
- [ ] Database connection pooling configured
- [ ] Logging levels set appropriately
- [ ] Health check endpoints tested

### 2. External Services
- [ ] Domain purchased (e.g., `signalcopilot.com`)
- [ ] SendGrid account created and verified
- [ ] NewsAPI key obtained (optional, has free tier)
- [ ] Finnhub key obtained (optional, has free tier)
- [ ] GitHub repository set up

### 3. Security
- [ ] JWT secret generated (32+ chars, secure random)
- [ ] Hangfire dashboard password set
- [ ] Database credentials secured
- [ ] API keys rotated from development values
- [ ] CORS allowlist configured
- [ ] Rate limiting configured (future)

---

## Step 1: Database Setup (Neon or Supabase)

### Option A: Neon (Recommended)

**Why Neon:**
- Generous free tier (0.5 GB storage, autosuspend after inactivity)
- Database branching for staging/preview environments
- Fast cold starts
- Built-in connection pooling

**Setup Steps:**

1. **Create Neon Account**
   - Go to https://neon.tech
   - Sign up with GitHub

2. **Create Project**
   ```
   Project Name: signalcopilot-prod
   Region: Choose closest to your backend (e.g., US East for Render US-East)
   Postgres Version: 15
   ```

3. **Get Connection String**
   ```
   Navigate to Dashboard → Connection Details → Copy
   Format: postgresql://user:password@ep-xxx.us-east-2.aws.neon.tech/signalcopilot?sslmode=require
   ```

4. **Create Staging Branch (Optional)**
   ```
   In Neon dashboard → Branches → New Branch
   Name: staging
   Parent: main
   ```
   This gives you a copy of production data for testing!

5. **Configure Connection Pooling**
   ```
   In Neon: Settings → Connection Pooling → Enable
   Use the pooled connection string for your API
   ```

### Option B: Supabase

**Why Supabase:**
- More generous free tier (500 MB storage, 2 GB bandwidth)
- Includes authentication (if you want to migrate from Identity later)
- Built-in object storage
- Real-time subscriptions (future use)

**Setup Steps:**

1. **Create Supabase Project**
   - Go to https://supabase.com
   - New Project → Name: signalcopilot-prod
   - Generate secure password

2. **Get Connection String**
   ```
   Settings → Database → Connection String → URI
   Format: postgresql://postgres:[PASSWORD]@db.xxx.supabase.co:5432/postgres
   ```

3. **Enable Connection Pooling**
   ```
   Use the "Connection pooling" string instead of "Direct connection"
   Port 6543 with pgbouncer
   ```

### Database Configuration

**Update `appsettings.Production.json`:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=ep-xxx.us-east-2.aws.neon.tech;Database=signalcopilot;Username=user;Password=xxx;SSL Mode=Require;Trust Server Certificate=true;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=10"
  }
}
```

**Connection Pool Settings:**
- `Minimum Pool Size=0` - Allow autosuspend in Neon
- `Maximum Pool Size=10` - Prevent connection exhaustion
- `SSL Mode=Require` - Enforce TLS

---

## Step 2: Backend Deployment (Render/Railway/Fly.io)

### Option A: Render (Recommended for Simplicity)

**Why Render:**
- Simple UI, great for beginners
- Free tier ($0) or Starter ($7/mo)
- Auto-deploy from GitHub
- Built-in health checks
- Free TLS certificates

#### Setup Steps

**1. Create Render Account**
- Go to https://render.com
- Sign up with GitHub

**2. Create New Web Service**
```
Dashboard → New + → Web Service
Connect GitHub repository: ajinsunny/marketsignal
Branch: master
```

**3. Configure Service**
```
Name: signalcopilot-api
Region: Oregon (US West) or Ohio (US East) - match Neon region
Branch: master
Root Directory: (leave blank)
Runtime: Docker
Dockerfile Path: src/SignalCopilot.Api/Dockerfile
```

**4. Set Instance Type**
```
Free tier: 512 MB RAM, shared CPU (for testing)
Starter: $7/mo - 512 MB RAM, 0.5 CPU (recommended)
Standard: $25/mo - 2 GB RAM, 1 CPU (for production scale)
```

**5. Add Environment Variables**
```bash
# Database
DATABASE_URL=postgresql://user:password@ep-xxx.us-east-2.aws.neon.tech/signalcopilot?sslmode=require

# JWT
JWT_SECRET=your-super-secure-secret-key-minimum-32-characters
JWT_ISSUER=SignalCopilot.Api
JWT_AUDIENCE=SignalCopilot.Client
JWT_EXPIRATION_MINUTES=1440

# CORS
ALLOWED_ORIGINS=https://app.signalcopilot.com,https://signalcopilot.vercel.app

# SendGrid
SENDGRID_API_KEY=SG.xxx
SENDGRID_FROM_EMAIL=noreply@signalcopilot.com
SENDGRID_FROM_NAME=Signal Copilot

# News APIs (Optional)
NEWSAPI_KEY=your_newsapi_key
NEWSAPI_ENABLED=true
FINNHUB_API_KEY=your_finnhub_key
FINNHUB_ENABLED=true

# Alert Settings
ALERT_HIGH_IMPACT_THRESHOLD=0.7
ALERT_DAILY_DIGEST_TIME=09:00

# Hangfire
HANGFIRE_DASHBOARD_PASSWORD=your-secure-dashboard-password

# Environment
ASPNETCORE_ENVIRONMENT=Production
```

**6. Configure Health Check**
```
Health Check Path: /api/health
```

**7. Deploy**
```
Click "Create Web Service"
Render will:
- Clone your repo
- Build Docker image
- Deploy container
- Assign URL: https://signalcopilot-api.onrender.com
```

**8. Add Custom Domain**
```
Settings → Custom Domain → Add
Domain: api.signalcopilot.com
Follow DNS instructions (add CNAME)
```

### Option B: Railway

**Why Railway:**
- $5 credit free, then $0.000463/GB-hour
- Excellent DX, automatic deployments
- Built-in database option (but Neon is better)
- Easy env var management

#### Setup Steps

**1. Create Railway Account**
- Go to https://railway.app
- Sign up with GitHub

**2. Create New Project**
```
New Project → Deploy from GitHub repo
Select: ajinsunny/marketsignal
```

**3. Configure Service**
```
Settings →
  Root Directory: (leave blank)
  Build Command: (auto-detected from Dockerfile)
  Start Command: (auto-detected)

Dockerfile Path: src/SignalCopilot.Api/Dockerfile
```

**4. Add Environment Variables**
Same as Render (see above)

**5. Add Custom Domain**
```
Settings → Domains → Custom Domain
Enter: api.signalcopilot.com
Add CNAME to your DNS
```

**6. Configure Health Checks**
```
Settings → Health Check
Path: /api/health
Timeout: 30s
Interval: 60s
```

### Option C: Fly.io

**Why Fly.io:**
- Most control, runs on edge globally
- Free tier: 3 shared VMs, 3GB storage
- Best latency (runs close to users)
- Requires CLI and more config

#### Setup Steps

**1. Install Fly CLI**
```bash
curl -L https://fly.io/install.sh | sh
fly auth signup
```

**2. Initialize Fly App**
```bash
cd /Users/ajin/Documents/GitHub/marketsignal
fly launch --no-deploy
```

**3. Configure `fly.toml`** (auto-generated, edit as needed)
```toml
app = "signalcopilot-api"
primary_region = "iad" # US East

[build]
  dockerfile = "src/SignalCopilot.Api/Dockerfile"

[env]
  ASPNETCORE_ENVIRONMENT = "Production"
  ASPNETCORE_URLS = "http://0.0.0.0:8080"

[[services]]
  internal_port = 8080
  protocol = "tcp"

  [[services.ports]]
    handlers = ["http"]
    port = 80

  [[services.ports]]
    handlers = ["tls", "http"]
    port = 443

  [[services.http_checks]]
    interval = 60000
    timeout = 10000
    method = "GET"
    path = "/api/health"
```

**4. Set Secrets**
```bash
fly secrets set DATABASE_URL="postgresql://..."
fly secrets set JWT_SECRET="your-secret-key"
fly secrets set SENDGRID_API_KEY="SG.xxx"
fly secrets set ALLOWED_ORIGINS="https://app.signalcopilot.com"
```

**5. Deploy**
```bash
fly deploy
```

**6. Add Custom Domain**
```bash
fly certs add api.signalcopilot.com
# Follow DNS instructions
```

---

## Step 3: Frontend Deployment (Vercel)

**Why Vercel:**
- Zero-config Next.js deployment
- Automatic preview deployments for PRs
- Edge network (CDN)
- Free SSL
- Excellent performance

### Setup Steps

**1. Push to GitHub** (if not already)
```bash
cd /Users/ajin/Documents/GitHub/marketsignal
git add .
git commit -m "chore: prepare for production deployment"
git push origin master
```

**2. Create Vercel Account**
- Go to https://vercel.com
- Sign up with GitHub

**3. Import Project**
```
Dashboard → Add New → Project
Import Git Repository → Select marketsignal
Framework Preset: Next.js (auto-detected)
Root Directory: frontend
```

**4. Configure Build Settings**
```
Build Command: npm run build (auto-detected)
Output Directory: .next (auto-detected)
Install Command: npm install (auto-detected)
Node.js Version: 20.x
```

**5. Add Environment Variables**
```bash
# Production
NEXT_PUBLIC_API_URL=https://api.signalcopilot.com

# Preview (for PR deployments)
NEXT_PUBLIC_API_URL=https://signalcopilot-api-staging.onrender.com
```

**6. Deploy**
```
Click "Deploy"
Vercel will:
- Install dependencies
- Build Next.js app
- Deploy to edge
- Assign URL: https://marketsignal.vercel.app
```

**7. Add Custom Domain**
```
Project Settings → Domains → Add
Domain: app.signalcopilot.com
OR: signalcopilot.com (root)
Follow DNS instructions
```

**8. Configure Preview Deployments**
```
Settings → Git → Production Branch: master
Every PR gets a preview URL: marketsignal-git-<branch>.vercel.app
```

---

## Step 4: DNS Configuration

### Domain Setup (e.g., Namecheap, Cloudflare, Google Domains)

**DNS Records:**

```
# Root domain (optional, can redirect to app)
Type: A
Name: @
Value: 76.76.21.21 (Vercel IP)

# Frontend
Type: CNAME
Name: app
Value: cname.vercel-dns.com

# Backend API
Type: CNAME
Name: api
Value: signalcopilot-api.onrender.com (or Railway/Fly equivalent)

# WWW redirect (optional)
Type: CNAME
Name: www
Value: cname.vercel-dns.com
```

**Cloudflare Setup (Recommended for extra DDoS protection):**

1. Add domain to Cloudflare
2. Update nameservers at registrar
3. Set DNS records (proxy enabled)
4. SSL/TLS → Full (strict)
5. Page Rules → Always Use HTTPS

---

## Step 5: CORS Configuration

### Update Backend `Program.cs`

```csharp
// Get allowed origins from environment
var allowedOrigins = builder.Configuration["AllowedOrigins"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
    ?? new[] { "http://localhost:3000", "http://localhost:3002" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});
```

### Environment Variables

**Production:**
```bash
ALLOWED_ORIGINS=https://app.signalcopilot.com,https://signalcopilot.com
```

**Development + Preview:**
```bash
ALLOWED_ORIGINS=http://localhost:3000,http://localhost:3002,https://*.vercel.app
```

---

## Step 6: Hangfire Configuration for Production

### Security: Dashboard Authentication

**Update `Program.cs`:**

```csharp
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly string _dashboardPassword;

    public HangfireAuthorizationFilter(string dashboardPassword)
    {
        _dashboardPassword = dashboardPassword;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Option 1: Check JWT token
        var authHeader = httpContext.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader.Substring("Bearer ".Length);
            if (ValidateJwtToken(token))
            {
                return true;
            }
        }

        // Option 2: Check dashboard password header
        var dashboardKey = httpContext.Request.Headers["X-Hangfire-Dashboard-Key"].ToString();
        if (!string.IsNullOrEmpty(dashboardKey) && dashboardKey == _dashboardPassword)
        {
            return true;
        }

        // Option 3: Basic auth (browser prompt)
        var authHeaderBasic = httpContext.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authHeaderBasic) && authHeaderBasic.StartsWith("Basic "))
        {
            var encodedCreds = authHeaderBasic.Substring("Basic ".Length).Trim();
            var decodedCreds = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCreds));
            var creds = decodedCreds.Split(':', 2);

            if (creds.Length == 2 && creds[0] == "admin" && creds[1] == _dashboardPassword)
            {
                return true;
            }
        }

        // Challenge for basic auth
        httpContext.Response.StatusCode = 401;
        httpContext.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
        return false;
    }

    private bool ValidateJwtToken(string token)
    {
        // Implement JWT validation
        // (same logic as in your JWT middleware)
        return false; // Placeholder
    }
}

// Configure Hangfire
var hangfirePassword = builder.Configuration["HangfireSettings:DashboardPassword"]
    ?? throw new InvalidOperationException("Hangfire dashboard password not configured");

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter(hangfirePassword) },
    DashboardTitle = "Signal Copilot Jobs",
    StatsPollingInterval = 10000 // 10 seconds
});
```

### Access Hangfire Dashboard in Production

```bash
# Via browser (will prompt for basic auth)
https://api.signalcopilot.com/hangfire
Username: admin
Password: [your HANGFIRE_DASHBOARD_PASSWORD]

# Via API with header
curl https://api.signalcopilot.com/hangfire \
  -H "X-Hangfire-Dashboard-Key: your-password"
```

### Job Configuration

**Ensure recurring jobs are configured:**

```csharp
public class BackgroundJobsService
{
    public void ConfigureRecurringJobs()
    {
        // News fetch: Every 30 minutes
        RecurringJob.AddOrUpdate(
            "fetch-news",
            () => FetchNewsForAllTickers(),
            "*/30 * * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            }
        );

        // Daily digest: 9 AM UTC (adjust for your users' timezone)
        RecurringJob.AddOrUpdate(
            "daily-digest",
            () => GenerateDailyDigests(),
            "0 9 * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            }
        );

        // High-impact alerts: Every hour
        RecurringJob.AddOrUpdate(
            "high-impact-alerts",
            () => GenerateHighImpactAlerts(),
            "0 * * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            }
        );
    }
}
```

---

## Step 7: SendGrid Email Configuration

### Setup Steps

**1. Create SendGrid Account**
- Go to https://sendgrid.com
- Sign up (100 emails/day free)
- Verify email address

**2. Create API Key**
```
Settings → API Keys → Create API Key
Name: SignalCopilot Production
Permissions: Full Access (or Mail Send only)
Copy key (starts with SG.xxx)
```

**3. Verify Sender Identity**
```
Settings → Sender Authentication → Single Sender Verification
Email: noreply@signalcopilot.com (or your domain)
Verify email
```

**4. Set Up Domain Authentication (Optional but Recommended)**
```
Settings → Sender Authentication → Authenticate Your Domain
Domain: signalcopilot.com
Add DNS records (CNAME for SPF/DKIM)
```

**5. Add to Environment Variables**
```bash
SENDGRID_API_KEY=SG.xxx
SENDGRID_FROM_EMAIL=noreply@signalcopilot.com
SENDGRID_FROM_NAME=Signal Copilot
```

### Test Email Sending

```bash
# Trigger a test alert
curl -X POST https://api.signalcopilot.com/api/jobs/generate-digests \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

---

## Step 8: Health Checks & Monitoring

### Health Check Endpoints

**Backend exposes:**
- `/api/health` - Basic liveness check
- `/api/health/detailed` - Database connectivity, service status

**Configure monitoring:**

**Option A: UptimeRobot (Free)**
```
Monitor Type: HTTP(s)
URL: https://api.signalcopilot.com/api/health
Interval: 5 minutes
Alert When Down: Email/SMS
```

**Option B: Better Stack (Free tier)**
```
More features: response time, status page, incident management
```

**Option C: Render Built-in**
```
Render automatically monitors health checks
Alerts via email when service is down
```

### Logging

**Production Logging Setup:**

```json
// appsettings.Production.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "Microsoft.EntityFrameworkCore": "Warning",
      "Hangfire": "Information",
      "SignalCopilot": "Information"
    }
  }
}
```

**View Logs:**
- **Render**: Dashboard → Logs tab (real-time streaming)
- **Railway**: Deployment → Logs
- **Fly.io**: `fly logs`

**Optional: External logging (future):**
- Sentry (error tracking)
- Papertrail (log aggregation)
- Datadog (APM)

---

## Step 9: Security Hardening

### 1. Environment Variables

**Never commit these to Git:**
```bash
# .gitignore already includes
appsettings.*.json
.env
.env.local
```

### 2. JWT Secret

**Generate strong secret:**
```bash
openssl rand -base64 48
# Use output as JWT_SECRET
```

### 3. Rate Limiting (Future Enhancement)

```csharp
// Add to Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

app.UseRateLimiter();
```

### 4. HTTPS Only

```csharp
// appsettings.Production.json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:8080"
      }
    }
  }
}

// Program.cs
if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
```

### 5. Content Security Policy

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});
```

---

## Step 10: Deployment Checklist

### Pre-Deployment

- [ ] All environment variables configured
- [ ] Database migrations tested
- [ ] CORS origins verified
- [ ] Hangfire dashboard secured
- [ ] SendGrid verified and tested
- [ ] Health checks responding
- [ ] Logs configured
- [ ] Secrets rotated from dev values

### First Deployment

- [ ] Database created (Neon/Supabase)
- [ ] Backend deployed (Render/Railway/Fly)
- [ ] Frontend deployed (Vercel)
- [ ] DNS configured
- [ ] Custom domains added
- [ ] SSL certificates verified
- [ ] CORS tested (from production frontend)
- [ ] API health check passing
- [ ] Hangfire dashboard accessible
- [ ] Background jobs running

### Post-Deployment

- [ ] Create test user and add holdings
- [ ] Trigger manual news fetch
- [ ] Verify impacts are calculated
- [ ] Test email alert (if configured)
- [ ] Monitor logs for errors
- [ ] Set up uptime monitoring
- [ ] Document any issues
- [ ] Create rollback plan

### Ongoing Maintenance

- [ ] Monitor database size (Neon free tier: 0.5 GB)
- [ ] Check Render/Railway usage and costs
- [ ] Review logs weekly for errors
- [ ] Update dependencies monthly
- [ ] Rotate API keys quarterly
- [ ] Review and optimize Hangfire jobs
- [ ] Monitor SendGrid quota (100/day free)

---

## Step 11: Cost Estimation

### Free Tier Setup (Perfect for MVP/Testing)

| Service | Plan | Cost | Notes |
|---------|------|------|-------|
| **Neon** | Free | $0 | 0.5 GB storage, autosuspend |
| **Render** | Free | $0 | Spins down after inactivity, 750 hrs/mo |
| **Vercel** | Hobby | $0 | 100 GB bandwidth, unlimited requests |
| **SendGrid** | Free | $0 | 100 emails/day |
| **NewsAPI** | Free | $0 | 100 requests/day (sufficient for testing) |
| **Domain** | Namecheap | $12/yr | .com domain |
| **TOTAL** | | **$12/year** | |

**Free tier limitations:**
- Render free tier spins down after 15 min inactivity (30s cold start)
- Neon autosuspends after inactivity (0.5s cold start)
- NewsAPI free tier limited to 100 requests/day

### Paid Production Setup (Recommended for Launch)

| Service | Plan | Cost | Notes |
|---------|------|------|-------|
| **Neon** | Launch | $19/mo | 10 GB storage, no autosuspend |
| **Render** | Starter | $7/mo | Always-on, 512 MB RAM, 0.5 CPU |
| **Vercel** | Pro | $20/mo | (Optional) More team features |
| **SendGrid** | Essentials | $20/mo | 50k emails/mo (or stay on free) |
| **NewsAPI** | Developer | $449/mo | (Expensive! Consider alternatives) |
| **Domain** | Namecheap | $12/yr | .com domain |
| **TOTAL** | | **$46-515/mo** | Depends on NewsAPI |

**Cost optimization:**
- Start with free SendGrid (100 emails/day often sufficient)
- Use only SEC Edgar (free) initially, add paid news sources later
- Consider Finnhub ($0-99/mo) instead of NewsAPI ($449/mo)

### Scaling Costs (1000+ Users)

| Service | Plan | Cost | Notes |
|---------|------|------|-------|
| **Neon** | Scale | $69/mo | 50 GB storage, high availability |
| **Render** | Standard | $25/mo | 2 GB RAM, 1 CPU |
| **Vercel** | Pro | $20/mo | Covers heavy traffic |
| **SendGrid** | Pro | $90/mo | 1.5M emails/mo |
| **TOTAL** | | **$204/mo** | |

---

## Step 12: Staging Environment (Optional)

### Why Staging?

- Test changes before production
- Preview database migrations
- Catch deployment issues early

### Setup with Neon Branching

**1. Create Staging Branch**
```bash
# In Neon dashboard
Branches → New Branch → Name: staging
This creates a copy of production data!
```

**2. Deploy Backend Staging**
```bash
# In Render: duplicate service
Service Name: signalcopilot-api-staging
Environment: Staging
DATABASE_URL: [Neon staging branch connection string]
ALLOWED_ORIGINS: https://signalcopilot-git-staging.vercel.app
```

**3. Deploy Frontend Staging**
```bash
# Vercel auto-creates preview for each branch
git checkout -b staging
git push origin staging
# Vercel deploys to: marketsignal-git-staging.vercel.app
```

---

## Step 13: CI/CD Pipeline (Optional)

### GitHub Actions Workflow

**`.github/workflows/deploy.yml`:**

```yaml
name: Deploy to Production

on:
  push:
    branches: [master]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      - name: Run tests
        run: dotnet test

  deploy-backend:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Trigger Render Deploy
        run: |
          curl -X POST "${{ secrets.RENDER_DEPLOY_HOOK }}"

  deploy-frontend:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Trigger Vercel Deploy
        run: |
          curl -X POST "${{ secrets.VERCEL_DEPLOY_HOOK }}"
```

---

## Step 14: Rollback Plan

### If Deployment Fails

**Backend (Render/Railway):**
```
Dashboard → Deployments → Previous Deployment → Redeploy
```

**Frontend (Vercel):**
```
Deployments → Previous Deployment → Promote to Production
```

**Database:**
```
If migration failed:
1. Restore from Neon snapshot (automatic daily backups)
2. Or rollback migration: dotnet ef migrations remove
```

---

## Troubleshooting Common Issues

### Issue 1: CORS Error in Browser

**Symptom:** `Access to fetch blocked by CORS policy`

**Fix:**
```bash
# Verify ALLOWED_ORIGINS includes your frontend domain
# In Render: Settings → Environment → Add
ALLOWED_ORIGINS=https://app.signalcopilot.com,https://*.vercel.app
```

### Issue 2: Database Connection Timeout

**Symptom:** `Npgsql.NpgsqlException: Connection timed out`

**Fix:**
```bash
# Check connection string includes SSL
Host=xxx;SSL Mode=Require;Trust Server Certificate=true

# Verify Neon/Supabase allows connections from Render IPs (should be automatic)
```

### Issue 3: Hangfire Jobs Not Running

**Symptom:** Jobs show in dashboard but never execute

**Fix:**
```csharp
// Ensure Hangfire server is started
app.UseHangfireServer(); // Must be called in Program.cs

// Check worker count
services.AddHangfireServer(options => {
    options.WorkerCount = 5; // Increase if needed
});
```

### Issue 4: Cold Start Latency

**Symptom:** First request after inactivity takes 30+ seconds

**Fix:**
- Upgrade to paid tier (Render Starter $7/mo)
- Or use Railway (doesn't spin down as aggressively)
- Or implement health check pings every 14 minutes

---

## Quick Reference

### Important URLs

```bash
# Production
Frontend: https://app.signalcopilot.com
API: https://api.signalcopilot.com
API Docs: https://api.signalcopilot.com/swagger
Hangfire: https://api.signalcopilot.com/hangfire

# Staging
Frontend: https://signalcopilot-git-staging.vercel.app
API: https://signalcopilot-api-staging.onrender.com
```

### Key Commands

```bash
# Deploy backend (if using Fly.io)
fly deploy

# View logs
# Render: Dashboard → Logs
# Railway: Deployment → Logs
# Fly: fly logs

# Run migration
dotnet ef database update --project src/SignalCopilot.Api

# Test API health
curl https://api.signalcopilot.com/api/health

# Trigger background job
curl -X POST https://api.signalcopilot.com/api/jobs/fetch-news \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## Next Steps After Deployment

1. **Monitor for 24 hours**
   - Check logs for errors
   - Verify background jobs running
   - Test email delivery

2. **Set up alerts**
   - UptimeRobot for downtime
   - Render/Railway built-in alerts
   - Sentry for error tracking (optional)

3. **Optimize performance**
   - Add Redis caching (optional)
   - Implement response compression
   - Optimize database queries

4. **Scale as needed**
   - Upgrade Render/Railway instance
   - Add read replicas (Neon Scale plan)
   - Implement CDN for API (Cloudflare)

5. **Iterate**
   - Gather user feedback
   - Add features
   - Refine impact scoring algorithm

---

**Deployment Guide Version:** 1.0
**Last Updated:** October 21, 2025
**Estimated Setup Time:** 2-3 hours for first deployment

Good luck with your production launch! 🚀
