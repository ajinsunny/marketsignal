# AWS Console Deployment Guide - Step by Step

This guide walks you through deploying Signal Copilot using the AWS Console (web interface).

**Use this guide if:**
- Your AWS CLI user has limited permissions
- You prefer visual/GUI deployment
- This is your first AWS deployment

**Time Required:** ~45-60 minutes

## Prerequisites

- AWS Account with admin access
- GitHub account
- GitHub repository: `marketsignal`
- Docker Desktop installed

## Step 1: Create RDS PostgreSQL Database (15 minutes)

### 1.1 Navigate to RDS
1. Login to AWS Console: https://console.aws.amazon.com
2. Region: Select **US East (N. Virginia) us-east-1** (top right corner)
3. Search for **RDS** in the search bar
4. Click **"Create database"**

### 1.2 Configure Database Settings
**Engine options:**
- Engine type: **PostgreSQL**
- Edition: **PostgreSQL**
- Engine Version: **PostgreSQL 15.4-R1** (or latest 15.x)

**Templates:**
- Select: **Free tier** (for testing) or **Dev/Test** (for production)

**Settings:**
- DB instance identifier: `signalcopilot-db`
- Master username: `postgres`
- Master password: `pOh47BWeZt/0dl0J7FDiVA==` (or generate new one)
- Confirm password

**Instance configuration:**
- DB instance class: **db.t4g.micro** (free tier) or **db.t4g.small** (production)

**Storage:**
- Storage type: **General Purpose SSD (gp3)**
- Allocated storage: **20 GB**
- Enable storage autoscaling: **Yes**
- Maximum storage threshold: **100 GB**

**Connectivity:**
- Compute resource: **Don't connect to an EC2 compute resource**
- Network type: **IPv4**
- Virtual Private Cloud (VPC): **(default)**
- DB Subnet group: **(default)**
- Public access: **No** (important for security)
- VPC security group: **Create new**
- New VPC security group name: `signalcopilot-db-sg`
- Availability Zone: **No preference**

**Database authentication:**
- Database authentication options: **Password authentication**

**Additional configuration:**
- Initial database name: `signalcopilot` (IMPORTANT - don't skip this!)
- DB parameter group: **(default)**
- Option group: **(default)**
- Backup:
  - Enable automated backups: **Yes**
  - Backup retention period: **7 days**
  - Backup window: **No preference**
- Encryption: **Enable encryption** (checked)
- Performance Insights: **Enable** (optional, for monitoring)
- Monitoring: **Enable Enhanced monitoring** (optional)
- Log exports: *(leave unchecked)*
- Maintenance:
  - Enable auto minor version upgrade: **Yes**
  - Maintenance window: **No preference**
- Deletion protection: **Disable** (for testing) or **Enable** (for production)

### 1.3 Create Database
1. Click **"Create database"**
2. Wait ~8-10 minutes for database to be available
3. **Note the endpoint:**
   - Go to RDS → Databases → signalcopilot-db
   - Copy the **Endpoint** (e.g., `signalcopilot-db.xxxxx.us-east-1.rds.amazonaws.com`)
   - Copy the **Port** (usually `5432`)

### 1.4 Save Connection Information
Create a file `aws-deployment-info.txt`:
```
RDS Endpoint: signalcopilot-db.xxxxx.us-east-1.rds.amazonaws.com
RDS Port: 5432
RDS Database: signalcopilot
RDS Username: postgres
RDS Password: pOh47BWeZt/0dl0J7FDiVA==

Connection String:
Host=signalcopilot-db.xxxxx.us-east-1.rds.amazonaws.com;Port=5432;Database=signalcopilot;Username=postgres;Password=pOh47BWeZt/0dl0J7FDiVA==;SslMode=Require
```

## Step 2: Create ECR Repository & Push Docker Image (10 minutes)

### 2.1 Create ECR Repository
1. AWS Console → Search for **ECR** (Elastic Container Registry)
2. Click **"Get Started"** or **"Create repository"**
3. Settings:
   - Visibility: **Private**
   - Repository name: `signalcopilot-api`
   - Tag immutability: **Disabled**
   - Scan on push: **Enabled** (optional, for security)
   - Encryption: **AES-256** (default)
4. Click **"Create repository"**

### 2.2 Push Docker Image to ECR

**From your Mac terminal:**

```bash
# Set variables
export AWS_REGION=us-east-1
export AWS_ACCOUNT_ID=107792665903
export ECR_URI=$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/signalcopilot-api

# Authenticate Docker to ECR
aws ecr get-login-password --region $AWS_REGION | \
  docker login --username AWS --password-stdin $ECR_URI

# Build Docker image
cd /Users/ajin/Documents/GitHub/marketsignal
docker build -f Dockerfile.root -t signalcopilot-api:latest .

# Tag image
docker tag signalcopilot-api:latest $ECR_URI:latest

# Push to ECR
docker push $ECR_URI:latest

# Verify
aws ecr describe-images --repository-name signalcopilot-api
```

**If you get permission errors**, use the AWS Console:
1. Go to ECR → Repositories → signalcopilot-api
2. Click **"View push commands"**
3. Follow the 4 commands shown

**Save the ECR URI:**
```
ECR URI: 107792665903.dkr.ecr.us-east-1.amazonaws.com/signalcopilot-api:latest
```

## Step 3: Create IAM Role for App Runner (5 minutes)

### 3.1 Create IAM Role
1. AWS Console → Search for **IAM**
2. Left sidebar → **Roles** → Click **"Create role"**
3. **Trusted entity type:**
   - Select: **AWS service**
   - Use case: **App Runner** (search in the dropdown)
   - Click **"Next"**
4. **Add permissions:**
   - Search and select: **AWSAppRunnerServicePolicyForECRAccess**
   - Click **"Next"**
5. **Name, review, and create:**
   - Role name: `AppRunnerECRAccessRole`
   - Description: `Allows App Runner to access ECR images`
   - Click **"Create role"**

### 3.2 Note the Role ARN
1. Go to IAM → Roles → AppRunnerECRAccessRole
2. Copy the **ARN** (e.g., `arn:aws:iam::107792665903:role/AppRunnerECRAccessRole`)
3. Add to your `aws-deployment-info.txt`:
```
IAM Role ARN: arn:aws:iam::107792665903:role/AppRunnerECRAccessRole
```

## Step 4: Deploy Backend to AWS App Runner (10 minutes)

### 4.1 Create App Runner Service
1. AWS Console → Search for **App Runner**
2. Click **"Create service"**

### 4.2 Source and Deployment

**Step 1: Source and deployment**
- Repository type: **Container registry**
- Provider: **Amazon ECR**
- Container image URI: Click **"Browse"** → Select `signalcopilot-api` → Select `latest` tag
- ECR access role: **AppRunnerECRAccessRole** (created in Step 3)
- Deployment settings:
  - Deployment trigger: **Manual** (change to Automatic later if desired)
- Click **"Next"**

### 4.3 Configure Service

**Step 2: Configure service**

**Service settings:**
- Service name: `signalcopilot-api`

**Instance configuration:**
- CPU: **1 vCPU**
- Memory: **2 GB**
- Instance role: **Create new role** (or leave as default)

**Port:**
- Port: `8080`

Click **"Next"**

### 4.4 Configure Build (Skip)
- Click **"Next"** (we're using pre-built image)

### 4.5 Configure Health Check

**Health check:**
- Protocol: **HTTP**
- Path: `/api/health`
- Interval: **10 seconds**
- Timeout: **5 seconds**
- Healthy threshold: **1**
- Unhealthy threshold: **5**

Click **"Next"**

### 4.6 Configure Environment Variables

**Add environment variables:**

Click **"Add environment variable"** for each:

1. `ASPNETCORE_ENVIRONMENT` = `Production`
2. `ASPNETCORE_URLS` = `http://+:8080`
3. `ConnectionStrings__DefaultConnection` = `Host=YOUR_RDS_ENDPOINT;Port=5432;Database=signalcopilot;Username=postgres;Password=YOUR_PASSWORD;SslMode=Require`
   - **Replace YOUR_RDS_ENDPOINT with actual endpoint from Step 1**
   - **Replace YOUR_PASSWORD with actual password**
4. `JwtSettings__SecretKey` = Generate with: `openssl rand -base64 32`
5. `JwtSettings__Issuer` = `SignalCopilot.Api`
6. `JwtSettings__Audience` = `SignalCopilot.Client`
7. `AllowedOrigins` = `*` (we'll update this after deploying frontend)
8. `Hangfire__RequireAuthentication` = `false`

**Security settings:**
- Encryption: **AWS owned key** (default)

Click **"Next"**

### 4.7 Review and Create
1. Review all settings
2. Click **"Create & deploy"**
3. Wait ~5-10 minutes for deployment

### 4.8 Get App Runner URL
1. Go to App Runner → Services → signalcopilot-api
2. Copy the **Default domain** (e.g., `xxxxx.us-east-1.awsapprunner.com`)
3. Add to your `aws-deployment-info.txt`:
```
App Runner URL: https://xxxxx.us-east-1.awsapprunner.com
```

### 4.9 Test Backend
```bash
curl https://xxxxx.us-east-1.awsapprunner.com/api/health
```

Expected response:
```json
{
  "status": "healthy",
  "timestamp": "2025-10-16T...",
  "environment": "Production"
}
```

## Step 5: Run Database Migrations (5 minutes)

### Option A: From Local Machine (Temporary Public Access)

**Enable temporary public access:**
1. RDS → Databases → signalcopilot-db → Modify
2. Connectivity → Public access: **Yes**
3. Apply immediately: **Yes**
4. Wait ~2 minutes

**Update security group:**
1. RDS → signalcopilot-db → Connectivity & security tab
2. Click on the VPC security group
3. Inbound rules → Edit inbound rules
4. Add rule:
   - Type: **PostgreSQL**
   - Source: **My IP** (auto-fills your IP)
   - Save rules

**Run migrations:**
```bash
cd /Users/ajin/Documents/GitHub/marketsignal/src/SignalCopilot.Api

export ConnectionStrings__DefaultConnection="Host=YOUR_RDS_ENDPOINT;Port=5432;Database=signalcopilot;Username=postgres;Password=YOUR_PASSWORD;SslMode=Prefer"

dotnet ef database update
```

**Remove public access (IMPORTANT):**
1. RDS → signalcopilot-db → Modify
2. Public access: **No**
3. Apply immediately: **Yes**
4. Security group → Remove your IP rule

### Option B: Via App Runner (Recommended for Production)

Update App Runner startup command:
1. App Runner → signalcopilot-api → Configuration → Edit
2. Start command: `sh -c "dotnet ef database update && dotnet SignalCopilot.Api.dll"`
3. Deploy

## Step 6: Deploy Frontend to AWS Amplify (10 minutes)

### 6.1 Create GitHub Personal Access Token
1. Go to: https://github.com/settings/tokens
2. Click **"Generate new token (classic)"**
3. Note: `AWS Amplify Deployment`
4. Expiration: **90 days** (or your preference)
5. Scopes:
   - ✅ **repo** (all)
   - ✅ **admin:repo_hook** (all)
6. Click **"Generate token"**
7. **COPY THE TOKEN** (you won't see it again!)

### 6.2 Create Amplify App
1. AWS Console → Search for **AWS Amplify**
2. Click **"Get started"** → **"Host web app"**
3. **Connect Git repository:**
   - Git provider: **GitHub**
   - Click **"Continue"**
   - Authorize AWS Amplify (if first time)

### 6.3 Add Repository
1. **Recently updated repositories:**
   - Select: **marketsignal**
   - Branch: **master**
   - Check: **Connecting a monorepo? Pick a folder.**
     - Folder: `frontend`
   - Click **"Next"**

### 6.4 Configure Build Settings
**App settings:**
- App name: `signalcopilot`
- Environment: `production`

**Build settings:**
- Auto-detected framework: **Next.js - SSR**
- Build command: `npm run build`
- Base directory: `frontend`
- You can keep the auto-generated amplify.yml or use our custom one

**Advanced settings** (expand):
- Add environment variable:
  - Key: `NEXT_PUBLIC_API_URL`
  - Value: `https://YOUR_APP_RUNNER_URL` (from Step 4.8)

Click **"Next"**

### 6.5 Review and Deploy
1. Review settings
2. Click **"Save and deploy"**
3. Wait ~5-10 minutes for build and deployment

### 6.6 Get Amplify URL
1. Once deployed, note the **App URL** (e.g., `https://master.xxxxxx.amplifyapp.com`)
2. Add to `aws-deployment-info.txt`:
```
Amplify URL: https://master.xxxxxx.amplifyapp.com
```

## Step 7: Update Backend CORS (2 minutes)

### 7.1 Update Environment Variable
1. App Runner → signalcopilot-api
2. Configuration → Environment variables → Edit
3. Find `AllowedOrigins`
4. Change from `*` to your Amplify URL: `https://master.xxxxxx.amplifyapp.com`
5. Click **"Save changes"**
6. Click **"Deploy"** (redeploys with new config)
7. Wait ~3 minutes

## Step 8: Test Complete Deployment (5 minutes)

### 8.1 Test Backend
```bash
# Health check
curl https://YOUR_APP_RUNNER_URL/api/health

# Detailed health (with database)
curl https://YOUR_APP_RUNNER_URL/api/health/detailed

# Register user
curl -X POST https://YOUR_APP_RUNNER_URL/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "timezone": "America/New_York"
  }'

# Login
curl -X POST https://YOUR_APP_RUNNER_URL/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!"
  }'
```

### 8.2 Test Frontend
1. Open: `https://master.xxxxxx.amplifyapp.com`
2. Click **"Jump In"**
3. Upload portfolio screenshot
4. Verify:
   - Holdings appear
   - Portfolio Overview shows data
   - No "NaN%" errors
   - Impact Feed loads

### 8.3 Test Hangfire Dashboard
1. Open: `https://YOUR_APP_RUNNER_URL/hangfire`
2. Verify dashboard loads
3. Check for any failed jobs

## Step 9: Save All Deployment Information

Create final `aws-deployment-info.txt`:

```
=== AWS Signal Copilot Deployment ===
Date: [Current Date]
Region: us-east-1
Account: 107792665903

=== Frontend (AWS Amplify) ===
URL: https://master.xxxxxx.amplifyapp.com
App ID: [Your App ID]

=== Backend (AWS App Runner) ===
URL: https://xxxxx.us-east-1.awsapprunner.com
Service ARN: [Your Service ARN]

=== Database (Amazon RDS) ===
Endpoint: signalcopilot-db.xxxxx.us-east-1.rds.amazonaws.com
Port: 5432
Database: signalcopilot
Username: postgres
Password: pOh47BWeZt/0dl0J7FDiVA==

Connection String:
Host=signalcopilot-db.xxxxx.us-east-1.rds.amazonaws.com;Port=5432;Database=signalcopilot;Username=postgres;Password=pOh47BWeZt/0dl0J7FDiVA==;SslMode=Require

=== Container Registry (ECR) ===
Repository: signalcopilot-api
URI: 107792665903.dkr.ecr.us-east-1.amazonaws.com/signalcopilot-api

=== IAM ===
App Runner Role: arn:aws:iam::107792665903:role/AppRunnerECRAccessRole

=== JWT Secret ===
Secret Key: [Your Generated JWT Secret]

=== Security Groups ===
Database SG: signalcopilot-db-sg

=== GitHub ===
Repository: marketsignal
Branch: master
Personal Access Token: [Saved securely]

=== Costs (Estimated) ===
RDS db.t4g.micro: ~$15/month
App Runner: ~$10-20/month
Amplify: $0-5/month
Total: ~$25-40/month
```

## Next Steps

1. ✅ **Set up custom domain** (optional)
   - Frontend: Amplify → Domain management
   - Backend: App Runner → Custom domains

2. ✅ **Enable API keys** for news providers
   - Update App Runner environment variables
   - Add: `DataSources__NewsApi__ApiKey`, etc.

3. ✅ **Configure CloudWatch alarms**
   - Set up alerts for errors, high CPU, etc.

4. ✅ **Enable automatic deployments**
   - App Runner: Connect to GitHub for auto-deploy
   - Amplify: Already automatic on push

5. ✅ **Set up Secrets Manager** (recommended)
   - Move sensitive data from environment variables
   - Use AWS Secrets Manager references

6. ✅ **Enable logging aggregation**
   - CloudWatch Logs Insights
   - Set retention policies

7. ✅ **Create staging environment**
   - Duplicate setup for testing
   - Use different Amplify branch

## Troubleshooting

**Backend won't start:**
- Check App Runner logs: Service → Logs
- Verify ECR image exists and is correct
- Check environment variables are correct

**Database connection fails:**
- Verify security group allows App Runner
- Check connection string format
- Ensure SSL mode is `Require`

**Frontend can't reach backend:**
- Verify NEXT_PUBLIC_API_URL is correct
- Check CORS AllowedOrigins matches Amplify URL
- Test backend health endpoint directly

**Migrations fail:**
- Check database endpoint is accessible
- Verify credentials are correct
- Check security group rules

## Support

- AWS Support: https://console.aws.amazon.com/support
- AWS App Runner Docs: https://docs.aws.amazon.com/apprunner
- AWS Amplify Docs: https://docs.amplify.aws
- Amazon RDS Docs: https://docs.aws.amazon.com/rds

## Estimated Costs

| Service | Instance | Monthly Cost |
|---------|----------|--------------|
| RDS PostgreSQL | db.t4g.micro | ~$15 |
| App Runner | 1 vCPU, 2GB | ~$10-20 |
| Amplify | Free tier | $0-5 |
| ECR | < 500MB | $0 |
| **Total** | | **~$25-40** |

**Congratulations! Your Signal Copilot is now live on AWS! 🎉**
