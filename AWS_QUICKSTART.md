# AWS Quick Start Deployment - 30 Minutes

This guide deploys Signal Copilot to AWS in ~30 minutes using AWS CLI commands.

## Prerequisites

```bash
# Install AWS CLI
brew install awscli

# Configure AWS credentials
aws configure
# Enter: Access Key, Secret Key, Region (us-east-1), Format (json)

# Verify configuration
aws sts get-caller-identity
```

## Architecture

- **Backend**: AWS App Runner (containerized .NET API)
- **Frontend**: AWS Amplify (Next.js)
- **Database**: Amazon RDS PostgreSQL
- **Cost**: ~$30-85/month

## Deployment Steps

### 1. Create Database (10 min)

```bash
# Set variables
export DB_PASSWORD="$(openssl rand -base64 16)"
export DB_INSTANCE="signalcopilot-db"

# Create RDS PostgreSQL
aws rds create-db-instance \
  --db-instance-identifier $DB_INSTANCE \
  --db-instance-class db.t4g.micro \
  --engine postgres \
  --engine-version 15.4 \
  --master-username postgres \
  --master-user-password "$DB_PASSWORD" \
  --allocated-storage 20 \
  --db-name signalcopilot \
  --backup-retention-period 7 \
  --no-publicly-accessible \
  --storage-encrypted \
  --region us-east-1

# Wait for database (takes ~8-10 minutes)
echo "Waiting for database to be available..."
aws rds wait db-instance-available \
  --db-instance-identifier $DB_INSTANCE

# Get database endpoint
export DB_ENDPOINT=$(aws rds describe-db-instances \
  --db-instance-identifier $DB_INSTANCE \
  --query 'DBInstances[0].Endpoint.Address' \
  --output text)

echo "Database created at: $DB_ENDPOINT"
echo "Database password: $DB_PASSWORD"
echo "SAVE THIS PASSWORD - you'll need it later!"
```

### 2. Push Docker Image to ECR (5 min)

```bash
# Create ECR repository
aws ecr create-repository \
  --repository-name signalcopilot-api \
  --region us-east-1

# Get ECR URI
export ECR_URI=$(aws ecr describe-repositories \
  --repository-names signalcopilot-api \
  --query 'repositories[0].repositoryUri' \
  --output text \
  --region us-east-1)

# Authenticate Docker
aws ecr get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin $ECR_URI

# Build and push
cd /Users/ajin/Documents/GitHub/marketsignal
docker build -f Dockerfile.root -t signalcopilot-api:latest .
docker tag signalcopilot-api:latest $ECR_URI:latest
docker push $ECR_URI:latest

echo "Docker image pushed to: $ECR_URI:latest"
```

### 3. Deploy Backend to App Runner (5 min)

```bash
# Generate JWT secret
export JWT_SECRET="$(openssl rand -base64 32)"

# Create IAM role for App Runner
cat > /tmp/trust-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": {"Service": "build.apprunner.amazonaws.com"},
    "Action": "sts:AssumeRole"
  }]
}
EOF

aws iam create-role \
  --role-name AppRunnerECRAccessRole \
  --assume-role-policy-document file:///tmp/trust-policy.json \
  2>/dev/null || echo "Role already exists"

aws iam attach-role-policy \
  --role-name AppRunnerECRAccessRole \
  --policy-arn arn:aws:iam::aws:policy/service-role/AWSAppRunnerServicePolicyForECRAccess

export ROLE_ARN=$(aws iam get-role \
  --role-name AppRunnerECRAccessRole \
  --query 'Role.Arn' \
  --output text)

# Create connection string
export DB_CONNECTION="Host=$DB_ENDPOINT;Port=5432;Database=signalcopilot;Username=postgres;Password=$DB_PASSWORD;SslMode=Require"

# Deploy App Runner service
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
          "ConnectionStrings__DefaultConnection": "'"$DB_CONNECTION"'",
          "JwtSettings__SecretKey": "'"$JWT_SECRET"'",
          "JwtSettings__Issuer": "SignalCopilot.Api",
          "JwtSettings__Audience": "SignalCopilot.Client",
          "AllowedOrigins": "*",
          "Hangfire__RequireAuthentication": "false"
        }
      }
    },
    "AuthenticationConfiguration": {
      "AccessRoleArn": "'"$ROLE_ARN"'"
    },
    "AutoDeploymentsEnabled": false
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

# Wait for service (takes ~3-5 minutes)
echo "Waiting for App Runner service..."
sleep 180

# Get API URL
export API_URL=$(aws apprunner list-services \
  --query 'ServiceSummaryList[?ServiceName==`signalcopilot-api`].ServiceUrl' \
  --output text)

echo "Backend API deployed at: https://$API_URL"
```

### 4. Run Database Migrations (3 min)

```bash
# Option A: From local machine (requires temporary public access)
# Get security group ID
export SG_ID=$(aws rds describe-db-instances \
  --db-instance-identifier $DB_INSTANCE \
  --query 'DBInstances[0].VpcSecurityGroups[0].VpcSecurityGroupId' \
  --output text)

# Allow your IP
aws ec2 authorize-security-group-ingress \
  --group-id $SG_ID \
  --protocol tcp \
  --port 5432 \
  --cidr $(curl -s ifconfig.me)/32 \
  2>/dev/null || echo "Rule might already exist"

# Run migrations
cd src/SignalCopilot.Api
export ConnectionStrings__DefaultConnection="$DB_CONNECTION"
dotnet ef database update

# Remove public access
aws ec2 revoke-security-group-ingress \
  --group-id $SG_ID \
  --protocol tcp \
  --port 5432 \
  --cidr $(curl -s ifconfig.me)/32

echo "Database migrations completed"
```

### 5. Deploy Frontend to Amplify (5 min)

```bash
# Note: Requires GitHub Personal Access Token
# Generate at: https://github.com/settings/tokens
# Required scopes: repo, admin:repo_hook

echo "Enter your GitHub Personal Access Token:"
read -s GITHUB_TOKEN

# Create Amplify app
aws amplify create-app \
  --name signalcopilot \
  --repository https://github.com/YOUR_USERNAME/marketsignal \
  --oauth-token $GITHUB_TOKEN \
  --build-spec '{
    "version": 1,
    "applications": [{
      "appRoot": "frontend",
      "frontend": {
        "phases": {
          "preBuild": {"commands": ["npm ci"]},
          "build": {"commands": ["npm run build"]}
        },
        "artifacts": {
          "baseDirectory": ".next",
          "files": ["**/*"]
        }
      }
    }]
  }' \
  --region us-east-1

# Get app ID
export AMPLIFY_APP_ID=$(aws amplify list-apps \
  --query 'apps[?name==`signalcopilot`].appId' \
  --output text)

# Create master branch
aws amplify create-branch \
  --app-id $AMPLIFY_APP_ID \
  --branch-name master \
  --enable-auto-build \
  --environment-variables NEXT_PUBLIC_API_URL=https://$API_URL

# Start deployment
aws amplify start-job \
  --app-id $AMPLIFY_APP_ID \
  --branch-name master \
  --job-type RELEASE

# Get Amplify URL
export FRONTEND_URL="https://master.$AMPLIFY_APP_ID.amplifyapp.com"
echo "Frontend will be available at: $FRONTEND_URL"
echo "(Build takes ~5-10 minutes)"
```

### 6. Update CORS (2 min)

```bash
# Wait for frontend build to complete
echo "Waiting for frontend build..."
sleep 300

# Update App Runner with Amplify URL
aws apprunner update-service \
  --service-arn $(aws apprunner list-services \
    --query 'ServiceSummaryList[?ServiceName==`signalcopilot-api`].ServiceArn' \
    --output text) \
  --source-configuration '{
    "ImageRepository": {
      "ImageConfiguration": {
        "RuntimeEnvironmentVariables": {
          "AllowedOrigins": "'"$FRONTEND_URL"'"
        }
      }
    }
  }' \
  --region us-east-1

echo "CORS updated with frontend URL"
```

## Verification

```bash
# Test backend health
curl https://$API_URL/api/health

# Test database connectivity
curl https://$API_URL/api/health/detailed

# Open frontend
echo "Frontend URL: $FRONTEND_URL"
echo "Backend URL: https://$API_URL"
```

## Save These URLs!

```bash
cat > deployment-info.txt <<EOF
=== Deployment Information ===
Frontend URL: $FRONTEND_URL
Backend API URL: https://$API_URL
Database Endpoint: $DB_ENDPOINT
Database Password: $DB_PASSWORD
JWT Secret: $JWT_SECRET

Amplify App ID: $AMPLIFY_APP_ID
ECR Repository: $ECR_URI
RDS Instance: $DB_INSTANCE

Date: $(date)
EOF

echo "Deployment info saved to: deployment-info.txt"
cat deployment-info.txt
```

## Alternative: Manual Deployment via Console

If you prefer using the AWS Console:

1. **RDS**: Console → RDS → Create database → PostgreSQL
2. **ECR**: Console → ECR → Create repository → Push image
3. **App Runner**: Console → App Runner → Create service → Select ECR image
4. **Amplify**: Console → Amplify → New app → Connect GitHub

See `AWS_DEPLOYMENT.md` for detailed console instructions.

## Monitoring

```bash
# View App Runner logs
aws logs tail /aws/apprunner/signalcopilot-api/service --follow

# Check service status
aws apprunner describe-service \
  --service-arn $(aws apprunner list-services \
    --query 'ServiceSummaryList[?ServiceName==`signalcopilot-api`].ServiceArn' \
    --output text)
```

## Troubleshooting

**App Runner deployment fails:**
```bash
# Check logs
aws logs tail /aws/apprunner/signalcopilot-api/service

# Verify ECR image exists
aws ecr describe-images --repository-name signalcopilot-api
```

**Database connection fails:**
```bash
# Verify endpoint
aws rds describe-db-instances \
  --db-instance-identifier $DB_INSTANCE

# Check security group
aws rds describe-db-instances \
  --db-instance-identifier $DB_INSTANCE \
  --query 'DBInstances[0].VpcSecurityGroups'
```

**CORS errors:**
- Verify `AllowedOrigins` matches Amplify URL
- Redeploy App Runner after changing environment variables

## Clean Up

```bash
# Delete all resources
aws amplify delete-app --app-id $AMPLIFY_APP_ID
aws apprunner delete-service \
  --service-arn $(aws apprunner list-services \
    --query 'ServiceSummaryList[?ServiceName==`signalcopilot-api`].ServiceArn' \
    --output text)
aws rds delete-db-instance \
  --db-instance-identifier $DB_INSTANCE \
  --skip-final-snapshot
aws ecr delete-repository \
  --repository-name signalcopilot-api \
  --force
```

## Next Steps

1. Set up custom domain (optional)
2. Add API keys for news providers
3. Configure CloudWatch alarms
4. Enable automatic deployments from GitHub
5. Set up staging environment

## Support

See `AWS_DEPLOYMENT.md` for comprehensive documentation.
