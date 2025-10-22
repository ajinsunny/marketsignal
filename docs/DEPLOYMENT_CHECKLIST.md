# Signal Copilot - Production Deployment Checklist

Use this checklist to ensure a smooth production deployment.

## Pre-Deployment Tasks

### 1. Code Preparation
- [ ] All features tested locally
- [ ] All tests passing (`dotnet test`)
- [ ] No console.log statements in production code
- [ ] Environment variables documented
- [ ] CHANGELOG.md updated
- [ ] Version number bumped (if applicable)

### 2. Database Setup
- [ ] Neon/Supabase account created
- [ ] Production database created
- [ ] Connection string obtained
- [ ] Connection pooling enabled
- [ ] Staging branch created (optional)
- [ ] Backup schedule verified

### 3. External Services
- [ ] Domain purchased and configured
- [ ] SendGrid account created and verified
- [ ] SendGrid API key obtained
- [ ] Sender identity verified
- [ ] Domain authentication configured (SPF/DKIM)
- [ ] News API keys obtained (if using paid tiers)

### 4. Security
- [ ] JWT secret generated (32+ chars): `openssl rand -base64 48`
- [ ] Hangfire dashboard password set
- [ ] All dev/test API keys rotated
- [ ] No secrets in code or Git history
- [ ] CORS origins configured correctly
- [ ] HTTPS enforced
- [ ] Security headers configured

### 5. Backend Deployment (Render/Railway/Fly)
- [ ] Account created
- [ ] Repository connected to GitHub
- [ ] Dockerfile path configured
- [ ] Health check endpoint set (`/api/health`)
- [ ] Environment variables configured (see below)
- [ ] Instance type selected
- [ ] Region selected (match database region)
- [ ] Auto-deploy enabled

### 6. Frontend Deployment (Vercel)
- [ ] Account created
- [ ] Repository imported
- [ ] Framework preset: Next.js
- [ ] Root directory: `frontend`
- [ ] Environment variables configured
- [ ] Production domain added
- [ ] Preview deployments enabled

### 7. DNS Configuration
- [ ] A/CNAME record for root domain
- [ ] CNAME for `app` subdomain → Vercel
- [ ] CNAME for `api` subdomain → Render/Railway/Fly
- [ ] SSL certificates verified
- [ ] DNS propagation confirmed (24-48 hours)

---

## Environment Variables Checklist

### Backend Required Variables
```bash
✓ DATABASE_URL              # Neon/Supabase connection string
✓ JWT_SECRET                # Min 32 chars, secure random
✓ JWT_ISSUER                # SignalCopilot.Api
✓ JWT_AUDIENCE              # SignalCopilot.Client
✓ JWT_EXPIRATION_MINUTES    # 1440 (24 hours)
✓ ALLOWED_ORIGINS           # https://app.signalcopilot.com,https://*.vercel.app
✓ SENDGRID_API_KEY          # SG.xxx
✓ SENDGRID_FROM_EMAIL       # noreply@signalcopilot.com
✓ SENDGRID_FROM_NAME        # Signal Copilot
✓ HANGFIRE_DASHBOARD_PASSWORD # Secure password for /hangfire
✓ ASPNETCORE_ENVIRONMENT    # Production
✓ ASPNETCORE_URLS           # http://0.0.0.0:8080
```

### Backend Optional Variables
```bash
□ NEWSAPI_KEY               # NewsAPI.org key
□ NEWSAPI_ENABLED           # true/false
□ FINNHUB_API_KEY           # Finnhub key
□ FINNHUB_ENABLED           # true/false
□ ALPHAVANTAGE_API_KEY      # AlphaVantage key
□ ALPHAVANTAGE_ENABLED      # true/false
□ OPENAI_API_KEY            # OpenAI key (for advanced NLP)
□ OPENAI_ENABLED            # true/false
✓ SECEDGAR_ENABLED          # true (always free)
✓ ALERT_HIGH_IMPACT_THRESHOLD # 0.7
✓ ALERT_DAILY_DIGEST_TIME   # 09:00
✓ HANGFIRE_WORKER_COUNT     # 5
```

### Frontend Required Variables
```bash
✓ NEXT_PUBLIC_API_URL       # https://api.signalcopilot.com
```

### Frontend Optional Variables
```bash
□ NEXT_PUBLIC_GA_ID         # Google Analytics
□ NEXT_PUBLIC_SENTRY_DSN    # Sentry for error tracking
```

---

## Deployment Steps

### Phase 1: Database Migration

- [ ] 1. Connect to production database
  ```bash
  # Update connection string in appsettings.json or set env var
  export DATABASE_URL="postgresql://user:pass@host/db?sslmode=require"
  ```

- [ ] 2. Run migrations
  ```bash
  cd src/SignalCopilot.Api
  dotnet ef database update
  ```

- [ ] 3. Verify tables created
  ```bash
  # Connect to Neon/Supabase dashboard and check tables
  # Should see: AspNetUsers, Holdings, Articles, Signals, Impacts, Alerts, etc.
  ```

### Phase 2: Backend Deployment

#### Option A: Render
- [ ] 1. Create new Web Service
- [ ] 2. Connect GitHub repo
- [ ] 3. Configure Dockerfile path: `src/SignalCopilot.Api/Dockerfile`
- [ ] 4. Set environment variables (see above)
- [ ] 5. Set health check: `/api/health`
- [ ] 6. Click "Create Web Service"
- [ ] 7. Wait for build and deployment (~5-10 min)
- [ ] 8. Test health endpoint: `curl https://xxx.onrender.com/api/health`

#### Option B: Railway
- [ ] 1. Create new project from GitHub
- [ ] 2. Railway auto-detects Dockerfile
- [ ] 3. Set environment variables in dashboard
- [ ] 4. Deploy automatically triggers
- [ ] 5. Add custom domain in settings
- [ ] 6. Test health endpoint

#### Option C: Fly.io
- [ ] 1. Install Fly CLI: `curl -L https://fly.io/install.sh | sh`
- [ ] 2. Login: `fly auth login`
- [ ] 3. Launch app: `fly launch --no-deploy`
- [ ] 4. Set secrets: `fly secrets set DATABASE_URL="..."`
- [ ] 5. Deploy: `fly deploy`
- [ ] 6. Add custom domain: `fly certs add api.signalcopilot.com`

### Phase 3: Frontend Deployment (Vercel)

- [ ] 1. Import project from GitHub
- [ ] 2. Framework preset: Next.js (auto-detected)
- [ ] 3. Root directory: `frontend`
- [ ] 4. Set environment variable: `NEXT_PUBLIC_API_URL`
- [ ] 5. Click "Deploy"
- [ ] 6. Wait for build (~2-3 min)
- [ ] 7. Add custom domain: `app.signalcopilot.com`
- [ ] 8. Test: Open https://app.signalcopilot.com

### Phase 4: DNS Configuration

- [ ] 1. Add A record for root domain → Vercel IP
- [ ] 2. Add CNAME for `app` → `cname.vercel-dns.com`
- [ ] 3. Add CNAME for `api` → `xxx.onrender.com`
- [ ] 4. Wait for DNS propagation (5 min - 48 hours)
- [ ] 5. Test: `dig app.signalcopilot.com`
- [ ] 6. Test: `dig api.signalcopilot.com`

---

## Post-Deployment Verification

### Backend API Health Checks

- [ ] Health endpoint responding
  ```bash
  curl https://api.signalcopilot.com/api/health
  # Expected: {"status":"healthy","timestamp":"..."}
  ```

- [ ] Detailed health check
  ```bash
  curl https://api.signalcopilot.com/api/health/detailed
  # Should show database connection status
  ```

- [ ] Swagger documentation accessible
  ```bash
  open https://api.signalcopilot.com/swagger
  # Should load Swagger UI
  ```

- [ ] Hangfire dashboard accessible
  ```bash
  open https://api.signalcopilot.com/hangfire
  # Should prompt for basic auth
  # Username: admin, Password: [your HANGFIRE_DASHBOARD_PASSWORD]
  ```

### Frontend Verification

- [ ] Landing page loads
  ```bash
  open https://app.signalcopilot.com
  # Should show "Jump In" button
  ```

- [ ] API connection working
  ```bash
  # Click "Jump In" button
  # Should auto-login and redirect to dashboard
  ```

- [ ] No CORS errors in browser console
  ```bash
  # Open browser dev tools → Console
  # Should see no "blocked by CORS policy" errors
  ```

### Functional Testing

- [ ] User registration works
  ```bash
  # Test via Swagger or curl
  curl -X POST https://api.signalcopilot.com/api/auth/register \
    -H "Content-Type: application/json" \
    -d '{"email":"test@example.com","password":"Test1234"}'
  ```

- [ ] User login works and returns JWT
  ```bash
  curl -X POST https://api.signalcopilot.com/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@example.com","password":"Test1234"}'
  # Should return: {"token":"eyJhbG...","expiration":"..."}
  ```

- [ ] Add holding works
  ```bash
  curl -X POST https://api.signalcopilot.com/api/holdings \
    -H "Authorization: Bearer YOUR_TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"ticker":"AAPL","shares":100,"costBasis":150.00,"intent":"Hold"}'
  ```

- [ ] Portfolio image upload works
  ```bash
  # Test via UI: Dashboard → Upload Portfolio Image
  ```

- [ ] Background jobs running
  ```bash
  # Check Hangfire dashboard
  open https://api.signalcopilot.com/hangfire
  # Should see recurring jobs: fetch-news, daily-digest, high-impact-alerts
  ```

- [ ] News fetch job works
  ```bash
  # Trigger manually
  curl -X POST https://api.signalcopilot.com/api/jobs/fetch-news \
    -H "Authorization: Bearer YOUR_TOKEN"
  # Check logs for news fetching activity
  ```

- [ ] Impact calculation works
  ```bash
  # After news is fetched, check impacts
  curl https://api.signalcopilot.com/api/impacts \
    -H "Authorization: Bearer YOUR_TOKEN"
  # Should return impacts if news was found
  ```

- [ ] Email sending works (if SendGrid configured)
  ```bash
  # Trigger digest manually
  curl -X POST https://api.signalcopilot.com/api/jobs/generate-digests \
    -H "Authorization: Bearer YOUR_TOKEN"
  # Check email inbox
  ```

---

## Monitoring Setup

### Uptime Monitoring

- [ ] UptimeRobot configured
  - Monitor: https://api.signalcopilot.com/api/health
  - Interval: 5 minutes
  - Alert: Email

- [ ] OR Better Stack configured
  - More features, status page

### Error Tracking (Optional)

- [ ] Sentry configured
  - DSN added to environment variables
  - Test error: Trigger intentional 500 error
  - Verify error appears in Sentry dashboard

### Log Monitoring

- [ ] Backend logs accessible
  - Render: Dashboard → Logs
  - Railway: Deployment → Logs
  - Fly: `fly logs`

- [ ] Log retention configured
  - Check platform's log retention policy
  - Consider external log aggregation (Papertrail, Datadog)

### Performance Monitoring (Optional)

- [ ] Response time tracking
  - Use Better Stack or Datadog APM

- [ ] Database query performance
  - Neon analytics dashboard

---

## Rollback Plan

### If Backend Deployment Fails

- [ ] Rollback to previous deployment
  ```bash
  # Render: Dashboard → Deployments → Previous → Redeploy
  # Railway: Deployment → Previous → Redeploy
  # Fly: fly releases rollback
  ```

### If Frontend Deployment Fails

- [ ] Rollback to previous deployment
  ```bash
  # Vercel: Deployments → Previous → Promote to Production
  ```

### If Database Migration Fails

- [ ] Restore from backup
  ```bash
  # Neon: Branches → Restore from point-in-time
  # Supabase: Database → Backups → Restore
  ```

- [ ] OR Rollback migration
  ```bash
  dotnet ef database update <PreviousMigrationName>
  ```

---

## Cost Monitoring

### Initial Costs (First Month)

- [ ] Neon: $0 (free tier) or $19/mo (Launch)
- [ ] Render: $0 (free) or $7/mo (Starter)
- [ ] Vercel: $0 (Hobby) or $20/mo (Pro)
- [ ] SendGrid: $0 (100 emails/day)
- [ ] Domain: ~$12/year
- [ ] **Total: $0-46/month**

### Set Billing Alerts

- [ ] Neon: Set storage alert at 400 MB (80% of 0.5 GB free tier)
- [ ] Render: Monitor usage dashboard
- [ ] Vercel: Set bandwidth alert
- [ ] SendGrid: Monitor email quota

---

## Security Audit

- [ ] No secrets committed to Git
  ```bash
  git log --all --full-history --source -- '*appsettings*.json' '*env*'
  # Should not show production secrets
  ```

- [ ] HTTPS enforced everywhere
- [ ] CORS configured correctly (no wildcards in production)
- [ ] Hangfire dashboard password protected
- [ ] Database uses SSL (sslmode=require)
- [ ] JWT secret is strong (32+ chars)
- [ ] API keys rotated from development values
- [ ] No debug/verbose logging in production

---

## Documentation Updates

- [ ] README.md updated with production URLs
- [ ] CHANGELOG.md updated with deployment date
- [ ] Team notified of deployment
- [ ] Runbook created for common operations
- [ ] Incident response plan documented

---

## Final Checks

- [ ] All tests passing
- [ ] All environment variables set
- [ ] DNS propagated
- [ ] SSL certificates valid
- [ ] CORS working
- [ ] Health checks passing
- [ ] Background jobs running
- [ ] Logs showing no errors
- [ ] Monitoring alerts configured
- [ ] Team trained on production access
- [ ] Rollback plan tested (if possible)

---

## Success Criteria

Your deployment is successful when:

✅ Frontend loads at https://app.signalcopilot.com
✅ API health check returns 200 at https://api.signalcopilot.com/api/health
✅ User can register, login, and add holdings via UI
✅ Background jobs are running (check Hangfire dashboard)
✅ No CORS errors in browser console
✅ No errors in backend logs
✅ Email alerts deliver successfully (if configured)
✅ Monitoring alerts are active
✅ SSL certificates are valid
✅ All team members can access production systems

---

## Post-Launch Tasks (Week 1)

- [ ] Monitor logs daily for errors
- [ ] Check Hangfire dashboard daily for failed jobs
- [ ] Verify email delivery rate
- [ ] Monitor database size growth
- [ ] Collect user feedback
- [ ] Fix any critical bugs immediately
- [ ] Document any issues encountered
- [ ] Update runbook based on real-world operations

---

**Checklist Version:** 1.0
**Last Updated:** October 21, 2025

Good luck with your deployment! 🚀
