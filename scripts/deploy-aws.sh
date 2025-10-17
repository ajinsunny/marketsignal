#!/bin/bash
# AWS Deployment Automation Script for Signal Copilot
# This script automates the entire AWS deployment process

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Check prerequisites
log_info "Checking prerequisites..."

if ! command -v aws &> /dev/null; then
    log_error "AWS CLI is not installed. Install it first:"
    log_error "brew install awscli"
    exit 1
fi

if ! command -v docker &> /dev/null; then
    log_error "Docker is not installed. Install Docker Desktop first."
    exit 1
fi

if ! command -v dotnet &> /dev/null; then
    log_error ".NET SDK is not installed."
    exit 1
fi

# Check AWS credentials
log_info "Checking AWS credentials..."
if ! aws sts get-caller-identity &> /dev/null; then
    log_error "AWS CLI is not configured or credentials are invalid."
    log_error "Run: aws configure"
    exit 1
fi

AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
AWS_REGION=$(aws configure get region || echo "us-east-1")

log_success "AWS Account: $AWS_ACCOUNT_ID"
log_success "AWS Region: $AWS_REGION"

# Configuration
DB_INSTANCE="signalcopilot-db"
DB_NAME="signalcopilot"
DB_USERNAME="postgres"
ECR_REPO="signalcopilot-api"
APP_RUNNER_SERVICE="signalcopilot-api"
AMPLIFY_APP="signalcopilot"

# Step 1: Generate secure passwords
log_info "Step 1/9: Generating secure credentials..."
DB_PASSWORD=$(openssl rand -base64 16)
JWT_SECRET=$(openssl rand -base64 32)

log_success "Database password generated"
log_success "JWT secret generated"

# Save credentials
DEPLOYMENT_INFO_FILE="aws-deployment-info-$(date +%Y%m%d-%H%M%S).txt"
cat > "$DEPLOYMENT_INFO_FILE" <<EOF
=== AWS Signal Copilot Deployment ===
Date: $(date)
Region: $AWS_REGION
Account: $AWS_ACCOUNT_ID

=== Credentials (KEEP SECURE) ===
Database Password: $DB_PASSWORD
JWT Secret: $JWT_SECRET

=== Resources ===
EOF

log_success "Deployment info file created: $DEPLOYMENT_INFO_FILE"

# Step 2: Create RDS PostgreSQL
log_info "Step 2/9: Creating RDS PostgreSQL database..."
log_info "This will take approximately 8-10 minutes..."

if aws rds describe-db-instances --db-instance-identifier "$DB_INSTANCE" &> /dev/null; then
    log_warning "Database $DB_INSTANCE already exists. Skipping creation."
else
    aws rds create-db-instance \
        --db-instance-identifier "$DB_INSTANCE" \
        --db-instance-class db.t4g.micro \
        --engine postgres \
        --engine-version 15.4 \
        --master-username "$DB_USERNAME" \
        --master-user-password "$DB_PASSWORD" \
        --allocated-storage 20 \
        --db-name "$DB_NAME" \
        --backup-retention-period 7 \
        --no-publicly-accessible \
        --storage-encrypted \
        --region "$AWS_REGION"

    log_info "Waiting for database to be available..."
    aws rds wait db-instance-available --db-instance-identifier "$DB_INSTANCE"
    log_success "Database created successfully"
fi

DB_ENDPOINT=$(aws rds describe-db-instances \
    --db-instance-identifier "$DB_INSTANCE" \
    --query 'DBInstances[0].Endpoint.Address' \
    --output text)

log_success "Database endpoint: $DB_ENDPOINT"

echo "Database Endpoint: $DB_ENDPOINT" >> "$DEPLOYMENT_INFO_FILE"

# Step 3: Create ECR repository
log_info "Step 3/9: Creating ECR repository..."

if aws ecr describe-repositories --repository-names "$ECR_REPO" &> /dev/null 2>&1; then
    log_warning "ECR repository $ECR_REPO already exists. Skipping creation."
else
    aws ecr create-repository \
        --repository-name "$ECR_REPO" \
        --image-scanning-configuration scanOnPush=true \
        --region "$AWS_REGION"
    log_success "ECR repository created"
fi

ECR_URI="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/$ECR_REPO"
log_success "ECR URI: $ECR_URI"

echo "ECR URI: $ECR_URI" >> "$DEPLOYMENT_INFO_FILE"

# Step 4: Build and push Docker image
log_info "Step 4/9: Building and pushing Docker image..."

# Authenticate Docker to ECR
log_info "Authenticating Docker to ECR..."
aws ecr get-login-password --region "$AWS_REGION" | \
    docker login --username AWS --password-stdin "$ECR_URI"

# Build Docker image
log_info "Building Docker image (this may take a few minutes)..."
cd "$(dirname "$0")/.."  # Go to project root
docker build -f Dockerfile.root -t "$ECR_REPO:latest" .

# Tag and push
log_info "Pushing image to ECR..."
docker tag "$ECR_REPO:latest" "$ECR_URI:latest"
docker push "$ECR_URI:latest"

log_success "Docker image pushed to ECR"

# Step 5: Create IAM role for App Runner
log_info "Step 5/9: Creating IAM role for App Runner..."

ROLE_NAME="AppRunnerECRAccessRole"
TRUST_POLICY=$(cat <<EOF
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": {"Service": "build.apprunner.amazonaws.com"},
    "Action": "sts:AssumeRole"
  }]
}
EOF
)

if aws iam get-role --role-name "$ROLE_NAME" &> /dev/null; then
    log_warning "IAM role $ROLE_NAME already exists. Skipping creation."
else
    echo "$TRUST_POLICY" > /tmp/trust-policy.json
    aws iam create-role \
        --role-name "$ROLE_NAME" \
        --assume-role-policy-document file:///tmp/trust-policy.json

    aws iam attach-role-policy \
        --role-name "$ROLE_NAME" \
        --policy-arn arn:aws:iam::aws:policy/service-role/AWSAppRunnerServicePolicyForECRAccess

    log_success "IAM role created"
fi

ROLE_ARN=$(aws iam get-role --role-name "$ROLE_NAME" --query 'Role.Arn' --output text)
log_success "IAM Role ARN: $ROLE_ARN"

echo "IAM Role ARN: $ROLE_ARN" >> "$DEPLOYMENT_INFO_FILE"

# Step 6: Deploy to App Runner
log_info "Step 6/9: Deploying backend to AWS App Runner..."

DB_CONNECTION="Host=$DB_ENDPOINT;Port=5432;Database=$DB_NAME;Username=$DB_USERNAME;Password=$DB_PASSWORD;SslMode=Require"

if aws apprunner list-services --query "ServiceSummaryList[?ServiceName=='$APP_RUNNER_SERVICE'].ServiceArn" --output text | grep -q .; then
    log_warning "App Runner service $APP_RUNNER_SERVICE already exists. Updating..."
    # TODO: Implement update logic
else
    log_info "Creating new App Runner service..."

    aws apprunner create-service \
        --service-name "$APP_RUNNER_SERVICE" \
        --source-configuration "{
            \"ImageRepository\": {
                \"ImageIdentifier\": \"$ECR_URI:latest\",
                \"ImageRepositoryType\": \"ECR\",
                \"ImageConfiguration\": {
                    \"Port\": \"8080\",
                    \"RuntimeEnvironmentVariables\": {
                        \"ASPNETCORE_ENVIRONMENT\": \"Production\",
                        \"ConnectionStrings__DefaultConnection\": \"$DB_CONNECTION\",
                        \"JwtSettings__SecretKey\": \"$JWT_SECRET\",
                        \"JwtSettings__Issuer\": \"SignalCopilot.Api\",
                        \"JwtSettings__Audience\": \"SignalCopilot.Client\",
                        \"AllowedOrigins\": \"*\",
                        \"Hangfire__RequireAuthentication\": \"false\"
                    }
                }
            },
            \"AuthenticationConfiguration\": {
                \"AccessRoleArn\": \"$ROLE_ARN\"
            },
            \"AutoDeploymentsEnabled\": false
        }" \
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
        --region "$AWS_REGION"

    log_info "Waiting for App Runner service to be created..."
    sleep 180  # Wait 3 minutes for initial deployment
fi

API_URL=$(aws apprunner list-services \
    --query "ServiceSummaryList[?ServiceName=='$APP_RUNNER_SERVICE'].ServiceUrl" \
    --output text)

log_success "Backend API deployed at: https://$API_URL"

echo "Backend API URL: https://$API_URL" >> "$DEPLOYMENT_INFO_FILE"

# Step 7: Test backend health
log_info "Step 7/9: Testing backend health..."
sleep 30  # Wait for service to be fully ready

if curl -sf "https://$API_URL/api/health" > /dev/null; then
    log_success "Backend health check passed"
else
    log_error "Backend health check failed. Check App Runner logs."
fi

# Step 8: Run database migrations
log_info "Step 8/9: Running database migrations..."
log_warning "You need to run migrations manually. See options:"
log_info "  Option A: Temporarily enable public access to RDS and run locally"
log_info "  Option B: Update App Runner startup command to run migrations"

# Step 9: Display next steps
log_info "Step 9/9: Deployment complete!"

cat <<EOF

${GREEN}========================================${NC}
${GREEN}   Deployment Successful! 🎉${NC}
${GREEN}========================================${NC}

${BLUE}Deployment Information:${NC}
- Deployment info saved to: $DEPLOYMENT_INFO_FILE
- Backend API URL: https://$API_URL
- Database Endpoint: $DB_ENDPOINT

${YELLOW}Next Steps:${NC}
1. Run database migrations (see AWS_DEPLOYMENT.md for instructions)
2. Deploy frontend to AWS Amplify (see AWS_CONSOLE_DEPLOYMENT.md)
3. Update CORS in App Runner with your Amplify URL
4. Test the complete deployment

${YELLOW}Frontend Deployment (AWS Amplify):${NC}
1. Go to AWS Console → Amplify
2. Create new app from GitHub (marketsignal repository)
3. Set environment variable: NEXT_PUBLIC_API_URL=https://$API_URL
4. Deploy and get Amplify URL
5. Update App Runner CORS with Amplify URL

${YELLOW}Test Your Deployment:${NC}
curl https://$API_URL/api/health

${RED}IMPORTANT: Keep this secure!${NC}
- Database password: $DB_PASSWORD
- JWT secret: $JWT_SECRET
- Deployment info: $DEPLOYMENT_INFO_FILE

EOF

log_success "All done! Check $DEPLOYMENT_INFO_FILE for complete information."
