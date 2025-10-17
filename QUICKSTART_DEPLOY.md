# Quick Start Deployment Guide

This guide will walk you through deploying Signal Copilot to production in ~30 minutes.

## What We're Deploying

- **Backend API**: Railway (ASP.NET Core 8.0)
- **Frontend**: Vercel (Next.js 15)
- **Database**: Railway PostgreSQL

**Estimated Monthly Cost**: $10-20 (Railway) + $0 (Vercel free tier) = **$10-20/month**

## Prerequisites

- GitHub account
- Railway account (sign up at railway.app)
- Vercel account (sign up at vercel.com)
- Your code pushed to GitHub

## Step 1: Push Code to GitHub (5 min)

```bash
# Navigate to project root
cd /Users/ajin/Documents/GitHub/marketsignal

# Check git status
git status

# Add all production-ready files
git add .

# Commit changes
git commit -m "feat: production-ready deployment configuration

- Add production security (CORS, Hangfire auth)
- Add Dockerfile and deployment configs
- Add health check endpoints
- Add environment variable templates
"

# Push to GitHub
git push origin master
```

## Step 2: Deploy Database on Railway (5 min)

1. Go to https://railway.app/new
2. Click **"New Project"** → **"Provision PostgreSQL"**
3. Wait for database to provision (~1 minute)
4. Click on the PostgreSQL service
5. Go to **"Connect"** tab
6. Copy the **"Postgres Connection URL"**
7. Convert it to .NET format (or use the Railway format directly):
   ```
   From: postgresql://user:pass@host:port/db
   To: Host=host;Port=port;Database=db;Username=user;Password=pass;SslMode=Require
   ```
8. Save this connection string - you'll need it in the next step

## Step 3: Deploy Backend API on Railway (10 min)

1. In Railway dashboard, click **"New Project"**
2. Select **"Deploy from GitHub repo"**
3. Authorize Railway to access your GitHub
4. Select your **marketsignal** repository
5. Railway will detect the Dockerfile and start building

### Configure Environment Variables:

Click on your service → **"Variables"** tab → Add these variables:

**Required Variables:**
```bash
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<YOUR_POSTGRESQL_CONNECTION_STRING>
JwtSettings__SecretKey=<GENERATE_A_STRONG_32_CHAR_KEY>
AllowedOrigins=https://your-app-name.vercel.app
```

**Generate JWT Secret Key:**
```bash
# Run this in your terminal to generate a secure key
openssl rand -base64 32
```

**Optional Variables (for enhanced features):**
```bash
DataSources__NewsApi__ApiKey=your_newsapi_key
DataSources__NewsApi__Enabled=true
DataSources__Finnhub__ApiKey=your_finnhub_key
DataSources__Finnhub__Enabled=true
Hangfire__RequireAuthentication=false
```

6. Click **"Deploy"** (if not auto-deployed)
7. Wait for deployment to complete (~5 minutes)
8. Go to **"Settings"** → **"Networking"** → **"Generate Domain"**
9. Copy your API URL (e.g., `https://signalcopilot-api.up.railway.app`)

### Run Database Migrations:

1. In Railway dashboard, click on your API service
2. Go to **"Settings"** → click **"New Service Variable"**
3. Add: `RAILWAY_RUN_MIGRATIONS=true` (optional)
4. Or manually run migrations via Railway CLI:
   ```bash
   railway login
   railway link
   railway run dotnet ef database update --project src/SignalCopilot.Api
   ```

**Alternative - Quick Migration Script:**
Create a file `migrate.sh` in your project root:
```bash
#!/bin/bash
# Run this after first deployment to initialize database
railway login
railway link <YOUR_PROJECT_ID>
railway run dotnet ef database update --project src/SignalCopilot.Api
```

## Step 4: Deploy Frontend on Vercel (5 min)

1. Go to https://vercel.com/new
2. Click **"Import Git Repository"**
3. Select your **marketsignal** repository
4. Configure project:
   - **Framework Preset**: Next.js
   - **Root Directory**: `frontend` (select via "Edit" button)
   - **Build Command**: `npm run build` (default)
   - **Output Directory**: `.next` (default)

5. Add Environment Variable:
   - Key: `NEXT_PUBLIC_API_URL`
   - Value: `https://your-api-url.up.railway.app` (from Step 3)

6. Click **"Deploy"**
7. Wait for deployment (~3 minutes)
8. Copy your Vercel URL (e.g., `https://signalcopilot.vercel.app`)

## Step 5: Update Backend CORS (2 min)

1. Go back to Railway dashboard
2. Select your API service
3. Go to **"Variables"** tab
4. Update `AllowedOrigins` with your actual Vercel URL:
   ```
   AllowedOrigins=https://signalcopilot.vercel.app
   ```
5. Save changes (Railway will auto-redeploy)

## Step 6: Test Deployment (3 min)

### Test Backend Health:
```bash
curl https://your-api-url.railway.app/api/health
```

Expected response:
```json
{
  "status": "healthy",
  "timestamp": "2025-10-16T...",
  "environment": "Production"
}
```

### Test Detailed Health (with database):
```bash
curl https://your-api-url.railway.app/api/health/detailed
```

### Test Frontend:
1. Open your Vercel URL in browser
2. Click **"Jump In"** button
3. Upload a portfolio screenshot
4. Verify data loads correctly

### Test Authentication:
```bash
# Register a user
curl -X POST https://your-api-url.railway.app/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "timezone": "America/New_York"
  }'

# Login
curl -X POST https://your-api-url.railway.app/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!"
  }'
```

## Step 7: Monitor and Verify (Ongoing)

### Railway Dashboard:
- **Logs**: View real-time logs
- **Metrics**: CPU, Memory, Network usage
- **Deployments**: View deployment history

### Vercel Dashboard:
- **Analytics**: Page views, performance
- **Logs**: Function logs and errors

### Check Hangfire Dashboard:
- URL: `https://your-api-url.railway.app/hangfire`
- If `Hangfire__RequireAuthentication=true`, you'll need to login first

## Troubleshooting

### "Internal Server Error" on API calls:
```bash
# Check Railway logs
railway logs

# Common issues:
# 1. Database connection string incorrect
# 2. Migrations not run
# 3. JWT secret not set
```

### CORS errors in frontend:
- Verify `AllowedOrigins` matches your Vercel URL exactly
- No trailing slash in URL
- Redeploy backend after changing CORS config

### Database connection fails:
- Ensure connection string uses `SslMode=Require`
- Verify database is running in Railway
- Check firewall settings

### Frontend can't reach backend:
- Verify `NEXT_PUBLIC_API_URL` is set correctly in Vercel
- Check browser console for errors
- Ensure backend is deployed and healthy

## Optional: Custom Domain Setup

### Backend (Railway):
1. Go to Railway service → Settings → Networking
2. Click "Add Custom Domain"
3. Enter your domain (e.g., `api.yourdomain.com`)
4. Add CNAME record in your DNS:
   ```
   CNAME api.yourdomain.com → your-railway-domain.railway.app
   ```

### Frontend (Vercel):
1. Go to Vercel project → Settings → Domains
2. Click "Add Domain"
3. Enter your domain (e.g., `yourdomain.com`)
4. Follow Vercel's DNS setup instructions

### Update CORS after custom domain:
```bash
AllowedOrigins=https://yourdomain.com,https://signalcopilot.vercel.app
```

## Cost Optimization

### Free Tier Usage:
- Railway: $5/month credit (limited resources)
- Vercel: Free for hobby projects
- **Total: $5/month free credit available**

### If you exceed free tier:
- Railway: ~$10-20/month (API + Database)
- Consider Supabase free tier for PostgreSQL
- Optimize: Use Railway only for API, host DB elsewhere

## Next Steps

1. **Set up monitoring**: Add Application Insights or Sentry
2. **Enable news providers**: Add NewsAPI and Finnhub API keys
3. **Configure email**: Add SendGrid API key for notifications
4. **Add rate limiting**: Protect API from abuse
5. **Set up CI/CD**: Automate deployments on git push
6. **Custom domain**: Add your own domain name

## Support

- **Railway Docs**: https://docs.railway.app
- **Vercel Docs**: https://vercel.com/docs
- **Full Deployment Guide**: See DEPLOYMENT.md for detailed information

## Deployment Checklist

- [ ] Code pushed to GitHub
- [ ] PostgreSQL database created on Railway
- [ ] Backend deployed on Railway
- [ ] Environment variables configured
- [ ] Database migrations run
- [ ] Backend health check passes
- [ ] Frontend deployed on Vercel
- [ ] Frontend environment variable set
- [ ] Backend CORS updated with frontend URL
- [ ] Frontend can reach backend
- [ ] User registration/login works
- [ ] Portfolio upload works
- [ ] Hangfire dashboard accessible

**Congratulations! Your Signal Copilot is now live in production! 🚀**
