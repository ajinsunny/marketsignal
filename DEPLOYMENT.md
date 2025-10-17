# Signal Copilot - Production Deployment Guide

## Architecture Overview

**Stack:**
- Backend: ASP.NET Core 8.0 Web API
- Frontend: Next.js 15.5.5
- Database: PostgreSQL 15+
- Background Jobs: Hangfire
- Authentication: JWT Bearer Tokens

**Recommended Hosting:**
- Backend API: Railway.app or Fly.io
- Frontend: Vercel
- Database: Railway PostgreSQL or Supabase

## Prerequisites

1. **Required Accounts:**
   - Railway account (https://railway.app) or Fly.io account
   - Vercel account (https://vercel.com)
   - GitHub account (for CI/CD)

2. **Required API Keys:**
   - NewsAPI.org API key (optional, free tier available)
   - OpenAI API key (optional, for advanced features)
   - SendGrid API key (optional, for email notifications)
   - Finnhub API key (optional, free tier available)

3. **Tools:**
   - Docker Desktop (for local testing)
   - .NET 8.0 SDK
   - Node.js 18+

## Environment Variables

### Backend Environment Variables

Create these in your hosting platform (Railway/Fly.io):

```bash
# Database
ConnectionStrings__DefaultConnection=Host=YOUR_DB_HOST;Port=5432;Database=signalcopilot;Username=YOUR_USER;Password=YOUR_PASSWORD;SslMode=Require

# JWT Authentication (IMPORTANT: Generate a strong 32+ character key)
JwtSettings__SecretKey=YOUR_STRONG_SECRET_KEY_HERE_MINIMUM_32_CHARS
JwtSettings__Issuer=SignalCopilot.Api
JwtSettings__Audience=SignalCopilot.Client
JwtSettings__ExpirationMinutes=1440

# CORS (Update with your production frontend URL)
AllowedOrigins=https://your-frontend-domain.vercel.app

# News Providers (Optional)
DataSources__NewsApi__ApiKey=your_newsapi_key
DataSources__NewsApi__Enabled=true
DataSources__Finnhub__ApiKey=your_finnhub_key
DataSources__Finnhub__Enabled=true
DataSources__OpenAI__ApiKey=your_openai_key
DataSources__OpenAI__Enabled=false

# SendGrid (Optional)
SendGrid__ApiKey=your_sendgrid_key
SendGrid__FromEmail=noreply@yourdomain.com

# Hangfire Dashboard (Set to require authentication)
Hangfire__DashboardEnabled=true
Hangfire__RequireAuthentication=true

# Logging
ASPNETCORE_ENVIRONMENT=Production
Logging__LogLevel__Default=Information
Logging__LogLevel__Microsoft.AspNetCore=Warning
```

### Frontend Environment Variables

Create these in Vercel:

```bash
NEXT_PUBLIC_API_URL=https://your-backend-api.railway.app
```

## Step-by-Step Deployment

### Step 1: Prepare Backend for Production

1. **Update CORS Configuration** (Program.cs)
   - Restrict CORS to your frontend domain
   - Remove AllowAnyOrigin()

2. **Secure Hangfire Dashboard**
   - Implement proper authentication
   - Restrict access to authenticated users only

3. **Configure Production Logging**
   - Use structured logging
   - Configure log levels appropriately

### Step 2: Deploy Database

#### Option A: Railway PostgreSQL

1. Go to Railway.app dashboard
2. Click "New Project" → "Provision PostgreSQL"
3. Copy the connection string (DATABASE_URL)
4. Convert to .NET connection string format:
   ```
   Host=HOST;Port=5432;Database=DATABASE;Username=USER;Password=PASSWORD;SslMode=Require
   ```

#### Option B: Supabase

1. Create project at supabase.com
2. Get connection string from Settings → Database
3. Use the "Connection Pooling" connection string for better performance

### Step 3: Deploy Backend API

#### Railway Deployment:

1. **Create New Project:**
   ```bash
   # Push your code to GitHub first
   git add .
   git commit -m "Prepare for production deployment"
   git push origin master
   ```

2. **In Railway Dashboard:**
   - Click "New Project" → "Deploy from GitHub repo"
   - Select your repository
   - Railway will auto-detect .NET project

3. **Configure Environment Variables:**
   - Add all backend environment variables listed above
   - Use the "Variables" tab in Railway

4. **Configure Build:**
   - Railway auto-detects Dockerfile or .NET project
   - Build command: `dotnet build`
   - Start command: `dotnet run --project src/SignalCopilot.Api/SignalCopilot.Api.csproj`

5. **Run Migrations:**
   - After first deployment, access Railway shell
   - Run: `dotnet ef database update --project src/SignalCopilot.Api`

6. **Get API URL:**
   - Railway provides a public URL (e.g., `https://signalcopilot-api.up.railway.app`)
   - Copy this for frontend configuration

#### Alternative: Fly.io Deployment:

```bash
# Install flyctl
brew install flyctl

# Login
flyctl auth login

# Initialize app (from src/SignalCopilot.Api directory)
cd src/SignalCopilot.Api
flyctl launch

# Set secrets
flyctl secrets set ConnectionStrings__DefaultConnection="your_connection_string"
flyctl secrets set JwtSettings__SecretKey="your_secret_key"

# Deploy
flyctl deploy
```

### Step 4: Deploy Frontend

#### Vercel Deployment:

1. **Connect Repository:**
   - Go to vercel.com/new
   - Import your GitHub repository
   - Select the `frontend` directory as root

2. **Configure Build Settings:**
   - Framework Preset: Next.js
   - Build Command: `npm run build` (default)
   - Output Directory: `.next` (default)
   - Install Command: `npm install` (default)

3. **Environment Variables:**
   - Add `NEXT_PUBLIC_API_URL` with your backend Railway URL
   - Example: `https://signalcopilot-api.railway.app`

4. **Deploy:**
   - Click "Deploy"
   - Vercel will build and deploy automatically
   - Get your production URL (e.g., `https://signalcopilot.vercel.app`)

5. **Update Backend CORS:**
   - Go back to Railway
   - Update `AllowedOrigins` environment variable with your Vercel URL
   - Redeploy backend if needed

### Step 5: Post-Deployment Configuration

1. **Test Authentication:**
   ```bash
   # Test registration
   curl -X POST https://your-api.railway.app/api/auth/register \
     -H "Content-Type: application/json" \
     -d '{"email":"test@example.com","password":"Test123!","timezone":"America/New_York"}'

   # Test login
   curl -X POST https://your-api.railway.app/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"test@example.com","password":"Test123!"}'
   ```

2. **Verify Database Migrations:**
   - Check Railway logs
   - Ensure all tables are created
   - Verify Hangfire tables exist

3. **Configure Background Jobs:**
   - Access Hangfire dashboard: `https://your-api.railway.app/hangfire`
   - Verify recurring jobs are scheduled
   - Monitor job execution

4. **Test Frontend:**
   - Open your Vercel URL
   - Click "Jump In"
   - Upload a portfolio screenshot
   - Verify data flows correctly

## Security Checklist

- [ ] Strong JWT secret key (32+ characters, randomly generated)
- [ ] CORS restricted to frontend domain only
- [ ] Hangfire dashboard requires authentication
- [ ] Database uses SSL/TLS connection
- [ ] Environment variables set (not hardcoded)
- [ ] HTTPS enforced on all endpoints
- [ ] API keys secured in environment variables
- [ ] Sensitive data excluded from git (.env files in .gitignore)

## Monitoring & Maintenance

### Health Checks

Backend exposes standard health endpoints:
- `/api/health` - Basic health check
- `/hangfire` - Background jobs status

### Logging

- Railway/Fly.io provide built-in log aggregation
- Access logs via dashboard or CLI
- Configure log retention as needed

### Database Backups

- Railway: Automatic daily backups (paid plan)
- Supabase: Point-in-time recovery available
- Manual: Use `pg_dump` for custom backup schedule

### Scaling

**Backend:**
- Railway: Auto-scaling available on Pro plan
- Fly.io: Manual scaling via `flyctl scale`

**Frontend:**
- Vercel: Auto-scales automatically
- No configuration needed

**Database:**
- Monitor connection pool usage
- Consider read replicas for heavy read workloads
- Use connection pooling (PgBouncer) for high traffic

## Cost Estimates

### Free Tier (Development/Testing):
- Railway: $5/month free credit (limited resources)
- Vercel: Free for hobby projects (unlimited bandwidth)
- Database: Included in Railway free tier (1GB)
- **Total: $0-5/month**

### Production (Light Usage):
- Railway: ~$10-20/month (API + Database)
- Vercel: Free (stays in hobby tier with moderate traffic)
- **Total: ~$10-20/month**

### Production (Heavy Usage):
- Railway: ~$50-100/month (scaled API + larger database)
- Vercel: ~$20/month (Pro plan for team features)
- **Total: ~$70-120/month**

## Troubleshooting

### Common Issues:

1. **"Internal Server Error" on API:**
   - Check Railway logs: `railway logs`
   - Verify database connection string
   - Ensure migrations ran successfully

2. **CORS errors in frontend:**
   - Verify `AllowedOrigins` includes your Vercel URL
   - Check for trailing slashes (must match exactly)
   - Restart backend after changing CORS config

3. **JWT authentication fails:**
   - Verify `JwtSettings__SecretKey` is set
   - Check token expiration time
   - Ensure frontend sends token in Authorization header

4. **Hangfire jobs not running:**
   - Check Hangfire dashboard for errors
   - Verify database connection
   - Check server logs for worker startup

5. **Database connection fails:**
   - Verify SSL mode: `SslMode=Require`
   - Check firewall rules
   - Verify credentials

## Rollback Strategy

1. **Backend:**
   - Railway: Rollback via dashboard (Deployments → Previous version)
   - Fly.io: `flyctl releases list` → `flyctl releases rollback`

2. **Frontend:**
   - Vercel: Instant rollback via dashboard (Deployments → Redeploy)

3. **Database:**
   - Restore from backup
   - Railway: Use backup restore feature
   - Manual: `pg_restore` from backup file

## Support & Resources

- **Railway Docs:** https://docs.railway.app
- **Vercel Docs:** https://vercel.com/docs
- **Hangfire Docs:** https://docs.hangfire.io
- **ASP.NET Core Docs:** https://docs.microsoft.com/aspnet/core

## Next Steps

After successful deployment:
1. Set up custom domain (optional)
2. Configure email notifications (SendGrid)
3. Enable news providers (NewsAPI, Finnhub)
4. Set up monitoring and alerts
5. Configure automated backups
6. Implement rate limiting
7. Add analytics (Application Insights, Sentry)
