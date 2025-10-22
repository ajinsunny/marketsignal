# Signal Copilot - Quick Deploy Guide

**Goal:** Get Signal Copilot running in production in under 2 hours.

This is the fast-track deployment guide. For comprehensive details, see [PRODUCTION_DEPLOYMENT.md](PRODUCTION_DEPLOYMENT.md).

---

## Prerequisites (5 minutes)

- [ ] GitHub account
- [ ] Domain name (optional for testing, can use platform URLs)
- [ ] Credit card (for paid tiers, not required for free tier)

---

## Step 1: Database (Neon) - 10 minutes

1. **Sign up:** https://neon.tech → Sign up with GitHub
2. **Create project:**
   - Name: `signalcopilot-prod`
   - Region: US East
   - Postgres 15
3. **Get connection string:**
   - Dashboard → Connection Details → Copy
   - Save this! Format: `postgresql://user:password@ep-xxx.us-east-2.aws.neon.tech/signalcopilot?sslmode=require`

---

## Step 2: Backend (Render) - 20 minutes

1. **Sign up:** https://render.com → Sign up with GitHub

2. **Create Web Service:**
   - Dashboard → New + → Web Service
   - Connect GitHub: `ajinsunny/marketsignal`
   - Branch: `master`

3. **Configure:**
   ```
   Name: signalcopilot-api
   Region: Oregon (US West)
   Branch: master
   Runtime: Docker
   Dockerfile Path: src/SignalCopilot.Api/Dockerfile
   Instance Type: Free (or Starter $7/mo for no spin-down)
   ```

4. **Add Environment Variables:**
   ```bash
   # Required - Copy/paste these
   DATABASE_URL=postgresql://...  # From Step 1
   JWT_SECRET=$(openssl rand -base64 48)  # Generate this
   JWT_ISSUER=SignalCopilot.Api
   JWT_AUDIENCE=SignalCopilot.Client
   JWT_EXPIRATION_MINUTES=1440
   ALLOWED_ORIGINS=http://localhost:3000,https://*.vercel.app
   SENDGRID_API_KEY=SG.xxx  # Get from Step 4, or leave blank for now
   SENDGRID_FROM_EMAIL=noreply@yourdomain.com
   SENDGRID_FROM_NAME=Signal Copilot
   HANGFIRE_DASHBOARD_PASSWORD=$(openssl rand -base64 24)  # Generate this
   ASPNETCORE_ENVIRONMENT=Production
   ASPNETCORE_URLS=http://0.0.0.0:8080
   SECEDGAR_ENABLED=true
   ALERT_HIGH_IMPACT_THRESHOLD=0.7
   ```

5. **Set Health Check:** `/api/health`

6. **Deploy:** Click "Create Web Service" → Wait 5-10 minutes

7. **Test:**
   ```bash
   curl https://signalcopilot-api.onrender.com/api/health
   # Expected: {"status":"healthy",...}
   ```

---

## Step 3: Frontend (Vercel) - 15 minutes

1. **Sign up:** https://vercel.com → Sign up with GitHub

2. **Import Project:**
   - Dashboard → Add New → Project
   - Import Git Repository: `marketsignal`
   - Framework Preset: Next.js (auto-detected)
   - Root Directory: `frontend`

3. **Configure:**
   ```
   Build Command: npm run build (auto)
   Output Directory: .next (auto)
   Install Command: npm install (auto)
   ```

4. **Add Environment Variable:**
   ```bash
   NEXT_PUBLIC_API_URL=https://signalcopilot-api.onrender.com
   ```

5. **Deploy:** Click "Deploy" → Wait 2-3 minutes

6. **Test:** Open assigned URL (e.g., `marketsignal.vercel.app`)

---

## Step 4: SendGrid (Optional) - 10 minutes

1. **Sign up:** https://sendgrid.com → Sign up
2. **Create API Key:**
   - Settings → API Keys → Create API Key
   - Name: `SignalCopilot Production`
   - Permissions: Full Access
   - Copy key (starts with `SG.`)
3. **Verify Sender:**
   - Settings → Sender Authentication → Single Sender
   - Email: `noreply@yourdomain.com`
   - Verify email
4. **Update Render:**
   - Go back to Render → Environment → Edit
   - Set `SENDGRID_API_KEY=SG.xxx`
   - Trigger redeploy

---

## Step 5: Custom Domain (Optional) - 30 minutes

### DNS Records (at your registrar):

```
# Frontend
Type: CNAME
Name: app
Value: cname.vercel-dns.com

# Backend
Type: CNAME
Name: api
Value: signalcopilot-api.onrender.com
```

### Add to Platforms:

**Vercel:**
- Project → Settings → Domains → Add
- Domain: `app.yourdomain.com`

**Render:**
- Service → Settings → Custom Domain → Add
- Domain: `api.yourdomain.com`

### Update CORS:

**Render:**
- Environment → Edit `ALLOWED_ORIGINS`
- Change to: `https://app.yourdomain.com`
- Trigger redeploy

---

## Step 6: Verify Everything Works - 10 minutes

### Backend Health Check
```bash
curl https://api.yourdomain.com/api/health
# or
curl https://signalcopilot-api.onrender.com/api/health
```

### Frontend
- Open https://app.yourdomain.com (or Vercel URL)
- Click "Jump In"
- Should auto-login and load dashboard

### Add Test Holding
- Ticker: AAPL
- Shares: 100
- Cost Basis: 150
- Intent: Hold
- Click "Add Holding"

### Trigger News Fetch
- Scroll down to "Job Controls"
- Click "Fetch News Now"
- Wait 30 seconds
- Impacts should appear (if news found)

### Check Hangfire Dashboard
- Open https://api.yourdomain.com/hangfire
- Username: `admin`
- Password: [your HANGFIRE_DASHBOARD_PASSWORD]
- Should see 3 recurring jobs running

---

## Troubleshooting

### CORS Error in Browser
```bash
# In Render: Settings → Environment
# Update ALLOWED_ORIGINS to include your frontend URL
ALLOWED_ORIGINS=https://marketsignal.vercel.app,https://app.yourdomain.com
```

### Backend Health Check Fails
```bash
# Check logs in Render: Dashboard → Logs
# Common issues:
# - Database connection string wrong
# - Migrations not run (run manually: dotnet ef database update)
```

### Frontend Can't Connect to API
```bash
# In Vercel: Project Settings → Environment Variables
# Verify NEXT_PUBLIC_API_URL is correct
# Should be: https://signalcopilot-api.onrender.com (or custom domain)
```

### Jobs Not Running
```bash
# Check Hangfire dashboard: https://api.yourdomain.com/hangfire
# If jobs are pending but not processing:
# - Increase HANGFIRE_WORKER_COUNT in Render
# - Check logs for errors
```

---

## Cost Breakdown (Free Tier)

| Service | Free Tier | Limitations |
|---------|-----------|-------------|
| **Neon** | $0 | 0.5 GB storage, autosuspend after inactivity |
| **Render** | $0 | 750 hours/mo, spins down after 15 min inactivity |
| **Vercel** | $0 | 100 GB bandwidth, unlimited requests |
| **SendGrid** | $0 | 100 emails/day |
| **Domain** | ~$12/yr | From registrar (Namecheap, Google, etc.) |
| **TOTAL** | **$12/year** | Perfect for MVP/testing |

**Limitations of Free Tier:**
- Backend spins down after 15 minutes (30-second cold start on first request)
- Database autosuspends (0.5-second cold start)
- Good for testing, but upgrade to paid tier for production use

## Upgrade to Paid (Recommended for Production)

| Service | Paid Tier | Cost | Benefit |
|---------|-----------|------|---------|
| **Neon** | Launch | $19/mo | 10 GB, no autosuspend |
| **Render** | Starter | $7/mo | Always-on, no spin-down |
| **TOTAL** | | **$26/mo** | Production-ready |

---

## Next Steps

1. **Monitor for 24 hours:**
   - Check Render logs for errors
   - Verify Hangfire jobs running
   - Test email delivery (if configured)

2. **Set up monitoring:**
   - UptimeRobot: https://uptimerobot.com (free)
   - Monitor: https://api.yourdomain.com/api/health

3. **Add more holdings:**
   - Test with various tickers
   - Verify impacts calculated correctly

4. **Customize settings:**
   - Alert threshold (default: 0.7)
   - Daily digest time (default: 9 AM UTC)
   - Risk profile

5. **Optional enhancements:**
   - Enable NewsAPI/Finnhub (paid)
   - Add Google Analytics
   - Set up Sentry for error tracking
   - Add staging environment

---

## Quick Commands Reference

```bash
# Generate JWT secret
openssl rand -base64 48

# Generate Hangfire password
openssl rand -base64 24

# Test backend health
curl https://api.yourdomain.com/api/health

# Test backend detailed health
curl https://api.yourdomain.com/api/health/detailed

# Register user
curl -X POST https://api.yourdomain.com/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test1234"}'

# Login
curl -X POST https://api.yourdomain.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test1234"}'

# Trigger news fetch
curl -X POST https://api.yourdomain.com/api/jobs/fetch-news \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## Need Help?

- **Full Guide:** [PRODUCTION_DEPLOYMENT.md](PRODUCTION_DEPLOYMENT.md)
- **Checklist:** [DEPLOYMENT_CHECKLIST.md](DEPLOYMENT_CHECKLIST.md)
- **Architecture:** [ARCHITECTURE.md](ARCHITECTURE.md)
- **API Docs:** [API_REFERENCE.md](API_REFERENCE.md)

---

**Estimated Time:** 1-2 hours for first deployment
**Difficulty:** Intermediate
**Cost:** Free tier available, $26/mo for production

🚀 **You're ready to deploy!**
