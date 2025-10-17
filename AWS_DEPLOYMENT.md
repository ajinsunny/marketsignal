# Signal Copilot - AWS Deployment Guide

## AWS Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         AWS Cloud                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────┐         ┌──────────────────────────────┐     │
│  │ AWS Amplify  │────────▶│  AWS App Runner              │     │
│  │ (Next.js)    │         │  (ASP.NET Core API)          │     │
│  │ Frontend     │         │  - Auto-scaling              │     │
│  └──────────────┘         │  - HTTPS included            │     │
│                           │  - Container deployment       │     │
│                           └──────────┬───────────────────┘     │
│                                      │                           │
│                                      ▼                           │
│                           ┌──────────────────────┐              │
│                           │   AWS RDS            │              │
│                           │   PostgreSQL 15      │              │
│                           │   - Multi-AZ         │              │
│                           │   - Auto backups     │              │
│                           └──────────────────────┘              │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │        Supporting Services                                │  │
│  │  - ECR: Container registry                                │  │
│  │  - Secrets Manager: Database credentials & API keys       │  │
│  │  - CloudWatch: Logs & monitoring                          │  │
│  │  - IAM: Access control                                    │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

## Why AWS App Runner + Amplify?

### AWS App Runner (Backend):
- **Fully managed** container service
- **Automatic scaling** (0-25 instances)
- **Built-in HTTPS** and custom domains
- **Direct GitHub integration** for CI/CD
- **Pay per use** (scales to zero)
- **Health checks** included
- **Environment variable** management
- Perfect for containerized .NET applications

### AWS Amplify (Frontend):
- **Built for Next.js** with SSR support
- **Global CDN** via CloudFront
- **Automatic builds** from GitHub
- **Branch previews** for testing
- **Custom domains** with SSL
- **Environment variables** per branch
- **Performance optimized**

### AWS RDS PostgreSQL (Database):
- **Fully managed** PostgreSQL
- **Automated backups** (35 days retention)
- **Multi-AZ** for high availability
- **Encryption** at rest and in transit
- **Performance Insights**
- **Automatic minor version upgrades**

## Prerequisites

1. **AWS Account** (https://aws.amazon.com)
2. **AWS CLI** installed and configured
3. **GitHub account** with code pushed
4. **Docker Desktop** (for local testing)

Install AWS CLI:
```bash
# macOS
brew install awscli

# Configure with your credentials
aws configure
# Enter: Access Key ID, Secret Access Key, Region (us-east-1), Output format (json)
```

## Cost Estimate

### Development/Testing:
- **RDS db.t4g.micro**: ~$15/month (free tier: 750 hours/month for 12 months)
- **App Runner**: ~$10-15/month (pay per use)
- **Amplify**: ~$0-5/month (free tier: 1000 build minutes/month)
- **Data Transfer**: ~$5/month
- **Total**: **~$30-35/month** (after free tier: ~$15/month for first year)

### Production (Light):
- **RDS db.t4g.small**: ~$30/month
- **App Runner**: ~$25-40/month
- **Amplify**: ~$5-10/month
- **Secrets Manager**: ~$1/month
- **CloudWatch**: ~$5/month
- **Total**: **~$65-85/month**

### Production (Medium Scale):
- **RDS db.t4g.medium**: ~$60/month
- **App Runner**: ~$50-100/month
- **Amplify**: ~$10-20/month
- **Total**: **~$120-180/month**

## Step-by-Step Deployment

### Step 1: Set Up AWS CLI (5 minutes)

```bash
# Verify AWS CLI installation
aws --version

# Configure credentials
aws configure
# AWS Access Key ID: [Your Access Key]
# AWS Secret Access Key: [Your Secret Key]
# Default region name: us-east-1
# Default output format: json

# Test configuration
aws sts get-caller-identity
```

### Step 2: Create RDS PostgreSQL Database (15 minutes)

#### Option A: AWS Console (Recommended for first-time)

1. Go to AWS Console → RDS → Create database
2. Configuration:
   - **Engine**: PostgreSQL 15.x
   - **Templates**: Free tier (or Dev/Test for production)
   - **DB instance identifier**: `signalcopilot-db`
   - **Master username**: `postgres`
   - **Master password**: `[Generate strong password]`
   - **DB instance class**: db.t4g.micro (free tier) or db.t4g.small (production)
   - **Storage**: 20 GB (auto-scaling enabled)
   - **Multi-AZ**: No (free tier) or Yes (production)
   - **Public access**: No (for security)
   - **VPC security group**: Create new or use default
   - **Database name**: `signalcopilot`

3. Click "Create database" (takes ~10 minutes)
4. Note the **Endpoint** and **Port** (usually 5432)

#### Option B: AWS CLI (Faster)

```bash
# Create database instance
aws rds create-db-instance \
  --db-instance-identifier signalcopilot-db \
  --db-instance-class db.t4g.micro \
  --engine postgres \
  --engine-version 15.4 \
  --master-username postgres \
  --master-user-password YOUR_STRONG_PASSWORD \
  --allocated-storage 20 \
  --db-name signalcopilot \
  --backup-retention-period 7 \
  --no-publicly-accessible \
  --storage-encrypted \
  --region us-east-1

# Wait for database to be available (takes ~10 minutes)
aws rds wait db-instance-available \
  --db-instance-identifier signalcopilot-db

# Get database endpoint
aws rds describe-db-instances \
  --db-instance-identifier signalcopilot-db \
  --query 'DBInstances[0].Endpoint.Address' \
  --output text
```

### Step 3: Store Database Credentials in Secrets Manager (5 minutes)

```bash
# Create secret for database connection string
aws secretsmanager create-secret \
  --name signalcopilot/database \
  --description "SignalCopilot database connection string" \
  --secret-string '{
    "host": "YOUR_RDS_ENDPOINT",
    "port": "5432",
    "database": "signalcopilot",
    "username": "postgres",
    "password": "YOUR_DB_PASSWORD"
  }' \
  --region us-east-1

# Create secret for JWT key
aws secretsmanager create-secret \
  --name signalcopilot/jwt-secret \
  --description "JWT signing key" \
  --secret-string "$(openssl rand -base64 32)" \
  --region us-east-1

# Verify secrets
aws secretsmanager list-secrets --region us-east-1
```

### Step 4: Push Docker Image to Amazon ECR (10 minutes)

```bash
# Create ECR repository
aws ecr create-repository \
  --repository-name signalcopilot-api \
  --region us-east-1

# Get repository URI
export ECR_URI=$(aws ecr describe-repositories \
  --repository-names signalcopilot-api \
  --query 'repositories[0].repositoryUri' \
  --output text \
  --region us-east-1)

echo "ECR Repository: $ECR_URI"

# Authenticate Docker to ECR
aws ecr get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin $ECR_URI

# Build Docker image
cd /Users/ajin/Documents/GitHub/marketsignal
docker build -f Dockerfile.root -t signalcopilot-api:latest .

# Tag image
docker tag signalcopilot-api:latest $ECR_URI:latest

# Push to ECR
docker push $ECR_URI:latest
```

### Step 5: Deploy Backend to AWS App Runner (10 minutes)

#### Create IAM Role for App Runner:

```bash
# Create trust policy
cat > app-runner-trust-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Service": "build.apprunner.amazonaws.com"
      },
      "Action": "sts:AssumeRole"
    }
  ]
}
EOF

# Create IAM role
aws iam create-role \
  --role-name AppRunnerECRAccessRole \
  --assume-role-policy-document file://app-runner-trust-policy.json

# Attach ECR access policy
aws iam attach-role-policy \
  --role-name AppRunnerECRAccessRole \
  --policy-arn arn:aws:iam::aws:policy/service-role/AWSAppRunnerServicePolicyForECRAccess
```

#### Create App Runner Service:

```bash
# Get RDS endpoint
export DB_ENDPOINT=$(aws rds describe-db-instances \
  --db-instance-identifier signalcopilot-db \
  --query 'DBInstances[0].Endpoint.Address' \
  --output text)

# Get ECR role ARN
export ROLE_ARN=$(aws iam get-role \
  --role-name AppRunnerECRAccessRole \
  --query 'Role.Arn' \
  --output text)

# Create App Runner service
aws apprunner create-service \
  --service-name signalcopilot-api \
  --source-configuration '{
    "ImageRepository": {
      "ImageIdentifier": "'"$ECR_URI"':latest",
      "ImageRepositoryType": "ECR",
      "ImageConfiguration": {
        "Port": "8080",
        "RuntimeEnvironmentVariables": {
          "ASPNETCORE_ENVIRONMENT": "Production",
          "ConnectionStrings__DefaultConnection": "Host='"$DB_ENDPOINT"';Port=5432;Database=signalcopilot;Username=postgres;Password=YOUR_DB_PASSWORD;SslMode=Require",
          "JwtSettings__SecretKey": "YOUR_JWT_SECRET_FROM_SECRETS_MANAGER",
          "JwtSettings__Issuer": "SignalCopilot.Api",
          "JwtSettings__Audience": "SignalCopilot.Client",
          "AllowedOrigins": "https://main.YOUR_AMPLIFY_ID.amplifyapp.com",
          "Hangfire__RequireAuthentication": "false"
        }
      }
    },
    "AuthenticationConfiguration": {
      "AccessRoleArn": "'"$ROLE_ARN"'"
    },
    "AutoDeploymentsEnabled": true
  }' \
  --instance-configuration '{
    "Cpu": "1024",
    "Memory": "2048"
  }' \
  --health-check-configuration '{
    "Protocol": "HTTP",
    "Path": "/api/health",
    "Interval": 10,
    "Timeout": 5,
    "HealthyThreshold": 1,
    "UnhealthyThreshold": 5
  }' \
  --region us-east-1

# Wait for service to be running (takes ~5-10 minutes)
aws apprunner wait service-created \
  --service-arn $(aws apprunner list-services \
    --query 'ServiceSummaryList[0].ServiceArn' \
    --output text)

# Get App Runner service URL
export API_URL=$(aws apprunner describe-service \
  --service-arn $(aws apprunner list-services \
    --query 'ServiceSummaryList[0].ServiceArn' \
    --output text) \
  --query 'Service.ServiceUrl' \
  --output text)

echo "Backend API URL: https://$API_URL"
```

#### Alternative: Using AWS Console:

1. Go to AWS Console → App Runner → Create service
2. **Source**: Container registry → Amazon ECR
3. **Container image URI**: Select your ECR image
4. **Deployment trigger**: Automatic
5. **Service settings**:
   - **Service name**: signalcopilot-api
   - **Port**: 8080
   - **CPU**: 1 vCPU
   - **Memory**: 2 GB
6. **Environment variables**: Add all required variables
7. **Health check**:
   - **Protocol**: HTTP
   - **Path**: /api/health
8. Click "Create & deploy"

### Step 6: Run Database Migrations (5 minutes)

```bash
# Option A: Connect to RDS from local machine (requires public access temporarily)
# Update RDS security group to allow your IP

# Get your public IP
curl ifconfig.me

# Update RDS security group (replace sg-xxxxx with your security group ID)
aws ec2 authorize-security-group-ingress \
  --group-id sg-xxxxx \
  --protocol tcp \
  --port 5432 \
  --cidr $(curl -s ifconfig.me)/32

# Run migrations locally
cd src/SignalCopilot.Api
export ConnectionStrings__DefaultConnection="Host=$DB_ENDPOINT;Port=5432;Database=signalcopilot;Username=postgres;Password=YOUR_PASSWORD;SslMode=Require"
dotnet ef database update

# Remove public access after migration
aws ec2 revoke-security-group-ingress \
  --group-id sg-xxxxx \
  --protocol tcp \
  --port 5432 \
  --cidr $(curl -s ifconfig.me)/32
```

```bash
# Option B: Run migrations from App Runner (add startup command)
# In App Runner configuration, add startup command:
# dotnet ef database update && dotnet SignalCopilot.Api.dll
```

### Step 7: Deploy Frontend to AWS Amplify (10 minutes)

#### Option A: AWS Console (Recommended):

1. Go to AWS Console → AWS Amplify → Get Started → Host web app
2. **Connect repository**:
   - Select **GitHub**
   - Authorize AWS Amplify
   - Select repository: `marketsignal`
   - Branch: `master`

3. **Configure build settings**:
   - App name: `signalcopilot`
   - Environment: `production`
   - Build and test settings: Auto-detected (Next.js)
   - **Root directory**: `frontend`

4. **Advanced settings**:
   - Add environment variable:
     - Key: `NEXT_PUBLIC_API_URL`
     - Value: `https://YOUR_APP_RUNNER_URL` (from Step 5)

5. Click **Save and deploy**

6. Wait for build (~5-10 minutes)

7. Note your Amplify URL: `https://main.YOUR_AMPLIFY_ID.amplifyapp.com`

#### Option B: AWS CLI with GitHub:

```bash
# Create Amplify app
aws amplify create-app \
  --name signalcopilot \
  --repository https://github.com/YOUR_USERNAME/marketsignal \
  --oauth-token YOUR_GITHUB_PERSONAL_ACCESS_TOKEN \
  --region us-east-1

# Get app ID
export AMPLIFY_APP_ID=$(aws amplify list-apps \
  --query 'apps[?name==`signalcopilot`].appId' \
  --output text)

# Create branch
aws amplify create-branch \
  --app-id $AMPLIFY_APP_ID \
  --branch-name master \
  --enable-auto-build \
  --environment-variables \
    NEXT_PUBLIC_API_URL=https://$API_URL

# Start deployment
aws amplify start-job \
  --app-id $AMPLIFY_APP_ID \
  --branch-name master \
  --job-type RELEASE

# Get Amplify URL
aws amplify get-app \
  --app-id $AMPLIFY_APP_ID \
  --query 'app.defaultDomain' \
  --output text
```

### Step 8: Update Backend CORS with Frontend URL (2 minutes)

```bash
# Update App Runner service with Amplify URL
aws apprunner update-service \
  --service-arn $(aws apprunner list-services \
    --query 'ServiceSummaryList[0].ServiceArn' \
    --output text) \
  --source-configuration '{
    "ImageRepository": {
      "ImageConfiguration": {
        "RuntimeEnvironmentVariables": {
          "AllowedOrigins": "https://main.YOUR_AMPLIFY_ID.amplifyapp.com"
        }
      }
    }
  }' \
  --region us-east-1
```

### Step 9: Testing & Verification (5 minutes)

#### Test Backend Health:
```bash
curl https://YOUR_APP_RUNNER_URL/api/health
```

Expected response:
```json
{
  "status": "healthy",
  "timestamp": "2025-10-16T...",
  "environment": "Production"
}
```

#### Test Database Connectivity:
```bash
curl https://YOUR_APP_RUNNER_URL/api/health/detailed
```

#### Test Frontend:
1. Open `https://main.YOUR_AMPLIFY_ID.amplifyapp.com`
2. Click "Jump In"
3. Upload portfolio screenshot
4. Verify data loads

#### Test Authentication:
```bash
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

## Monitoring & Maintenance

### CloudWatch Logs:

```bash
# View App Runner logs
aws logs tail /aws/apprunner/signalcopilot-api/service --follow

# View RDS logs
aws rds describe-db-log-files \
  --db-instance-identifier signalcopilot-db
```

### CloudWatch Metrics:

1. Go to AWS Console → CloudWatch → Metrics
2. App Runner: CPU, Memory, Requests, Response time
3. RDS: CPU, Connections, Storage, IOPS

### Set Up CloudWatch Alarms:

```bash
# Create alarm for high API error rate
aws cloudwatch put-metric-alarm \
  --alarm-name signalcopilot-api-errors \
  --alarm-description "Alert when API error rate is high" \
  --metric-name 5XXError \
  --namespace AWS/AppRunner \
  --statistic Sum \
  --period 300 \
  --evaluation-periods 2 \
  --threshold 10 \
  --comparison-operator GreaterThanThreshold

# Create alarm for high database CPU
aws cloudwatch put-metric-alarm \
  --alarm-name signalcopilot-db-cpu \
  --alarm-description "Alert when database CPU is high" \
  --metric-name CPUUtilization \
  --namespace AWS/RDS \
  --statistic Average \
  --period 300 \
  --evaluation-periods 2 \
  --threshold 80 \
  --comparison-operator GreaterThanThreshold \
  --dimensions Name=DBInstanceIdentifier,Value=signalcopilot-db
```

## Custom Domain Setup (Optional)

### Backend (App Runner):

```bash
# Associate custom domain
aws apprunner associate-custom-domain \
  --service-arn $(aws apprunner list-services \
    --query 'ServiceSummaryList[0].ServiceArn' \
    --output text) \
  --domain-name api.yourdomain.com

# Add CNAME record in your DNS:
# CNAME api.yourdomain.com → YOUR_APP_RUNNER_URL
```

### Frontend (Amplify):

1. Go to Amplify Console → Domain management
2. Add domain: `yourdomain.com`
3. Follow DNS configuration instructions
4. SSL certificate auto-provisioned via ACM

## Security Best Practices

### 1. Use AWS Secrets Manager:
```bash
# Update App Runner to use Secrets Manager
aws apprunner update-service \
  --service-arn $(aws apprunner list-services \
    --query 'ServiceSummaryList[0].ServiceArn' \
    --output text) \
  --source-configuration '{
    "ImageRepository": {
      "ImageConfiguration": {
        "RuntimeEnvironmentSecrets": {
          "ConnectionStrings__DefaultConnection": "signalcopilot/database:connectionString",
          "JwtSettings__SecretKey": "signalcopilot/jwt-secret"
        }
      }
    }
  }'
```

### 2. Enable RDS Encryption:
- Already enabled if created with `--storage-encrypted`
- Use SSL/TLS: `SslMode=Require` in connection string

### 3. Restrict RDS Access:
- Never allow public access
- Use VPC security groups
- Only allow App Runner security group

### 4. Enable CloudTrail:
- Audit all AWS API calls
- Track configuration changes

### 5. IAM Least Privilege:
- App Runner role: only ECR access
- No root credentials

## Scaling

### App Runner Auto-Scaling:
```bash
# Configure auto-scaling
aws apprunner update-service \
  --service-arn $(aws apprunner list-services \
    --query 'ServiceSummaryList[0].ServiceArn' \
    --output text) \
  --auto-scaling-configuration-arn arn:aws:apprunner:us-east-1:YOUR_ACCOUNT:autoscalingconfiguration/signalcopilot-scaling/1 \
  --instance-configuration '{
    "Cpu": "2048",
    "Memory": "4096",
    "InstanceRoleArn": "YOUR_ROLE_ARN"
  }'
```

### RDS Scaling:
- Vertical: Change instance class (requires downtime)
- Horizontal: Read replicas for read-heavy workloads
- Storage: Auto-scaling enabled by default

## Backup & Disaster Recovery

### RDS Automated Backups:
- Daily snapshots (35-day retention)
- Point-in-time recovery
- Cross-region replication available

```bash
# Create manual snapshot
aws rds create-db-snapshot \
  --db-snapshot-identifier signalcopilot-snapshot-$(date +%Y%m%d) \
  --db-instance-identifier signalcopilot-db

# Restore from snapshot
aws rds restore-db-instance-from-db-snapshot \
  --db-instance-identifier signalcopilot-db-restored \
  --db-snapshot-identifier signalcopilot-snapshot-20251016
```

### App Runner:
- Deploy from tagged container images
- Keep previous images in ECR
- Instant rollback available

## Troubleshooting

### App Runner deployment fails:
```bash
# Check service status
aws apprunner describe-service \
  --service-arn YOUR_SERVICE_ARN

# View logs
aws logs tail /aws/apprunner/signalcopilot-api/service
```

### Database connection fails:
- Check security group rules
- Verify connection string
- Ensure SSL mode: `SslMode=Require`

### CORS errors:
- Verify `AllowedOrigins` matches Amplify URL
- No trailing slash in URL
- Redeploy App Runner after changes

## Clean Up (Delete All Resources)

```bash
# Delete Amplify app
aws amplify delete-app --app-id $AMPLIFY_APP_ID

# Delete App Runner service
aws apprunner delete-service \
  --service-arn $(aws apprunner list-services \
    --query 'ServiceSummaryList[0].ServiceArn' \
    --output text)

# Delete RDS instance
aws rds delete-db-instance \
  --db-instance-identifier signalcopilot-db \
  --skip-final-snapshot

# Delete ECR repository
aws ecr delete-repository \
  --repository-name signalcopilot-api \
  --force

# Delete secrets
aws secretsmanager delete-secret \
  --secret-id signalcopilot/database \
  --force-delete-without-recovery

aws secretsmanager delete-secret \
  --secret-id signalcopilot/jwt-secret \
  --force-delete-without-recovery
```

## Summary

**Total Deployment Time**: ~45-60 minutes

**AWS Services Used**:
- ✅ AWS App Runner (Backend API)
- ✅ AWS Amplify (Frontend)
- ✅ Amazon RDS PostgreSQL (Database)
- ✅ Amazon ECR (Container Registry)
- ✅ AWS Secrets Manager (Secrets)
- ✅ AWS CloudWatch (Monitoring)
- ✅ AWS IAM (Access Control)

**Monthly Cost**: ~$30-85/month (depending on usage)

**Next Steps**:
1. Review AWS_QUICKSTART.md for simplified commands
2. Set up custom domain
3. Configure API keys (NewsAPI, Finnhub, OpenAI)
4. Enable email notifications (SES or SendGrid)
5. Set up CloudWatch dashboards

## Support Resources

- AWS App Runner: https://docs.aws.amazon.com/apprunner/
- AWS Amplify: https://docs.amplify.aws/
- Amazon RDS: https://docs.aws.amazon.com/rds/
- AWS CLI: https://docs.aws.amazon.com/cli/
