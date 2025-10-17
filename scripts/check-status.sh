#!/bin/bash
# Check status of all AWS resources

set -e

AWS_REGION=$(aws configure get region || echo "us-east-1")
AWS_ACCOUNT=$(aws sts get-caller-identity --query Account --output text)

echo "========================================"
echo "  Signal Copilot AWS Status"
echo "========================================"
echo "Account: $AWS_ACCOUNT"
echo "Region: $AWS_REGION"
echo ""

# RDS Database
echo "--- RDS Database ---"
if aws rds describe-db-instances --db-instance-identifier signalcopilot-db &>/dev/null; then
    DB_STATUS=$(aws rds describe-db-instances \
        --db-instance-identifier signalcopilot-db \
        --query 'DBInstances[0].DBInstanceStatus' \
        --output text)
    DB_ENDPOINT=$(aws rds describe-db-instances \
        --db-instance-identifier signalcopilot-db \
        --query 'DBInstances[0].Endpoint.Address' \
        --output text)
    echo "Status: $DB_STATUS"
    echo "Endpoint: $DB_ENDPOINT"
else
    echo "Status: NOT FOUND"
fi
echo ""

# ECR Repository
echo "--- ECR Repository ---"
if aws ecr describe-repositories --repository-names signalcopilot-api &>/dev/null; then
    IMAGE_COUNT=$(aws ecr list-images \
        --repository-name signalcopilot-api \
        --query 'length(imageIds)' \
        --output text)
    ECR_URI="$AWS_ACCOUNT.dkr.ecr.$AWS_REGION.amazonaws.com/signalcopilot-api"
    echo "Status: EXISTS"
    echo "URI: $ECR_URI"
    echo "Images: $IMAGE_COUNT"
else
    echo "Status: NOT FOUND"
fi
echo ""

# App Runner Service
echo "--- App Runner Service ---"
SERVICE_ARN=$(aws apprunner list-services \
    --query "ServiceSummaryList[?ServiceName=='signalcopilot-api'].ServiceArn" \
    --output text 2>/dev/null || echo "")

if [ -n "$SERVICE_ARN" ]; then
    SERVICE_STATUS=$(aws apprunner describe-service \
        --service-arn "$SERVICE_ARN" \
        --query 'Service.Status' \
        --output text)
    SERVICE_URL=$(aws apprunner describe-service \
        --service-arn "$SERVICE_ARN" \
        --query 'Service.ServiceUrl' \
        --output text)
    echo "Status: $SERVICE_STATUS"
    echo "URL: https://$SERVICE_URL"

    # Test health endpoint
    echo -n "Health Check: "
    if curl -sf "https://$SERVICE_URL/api/health" > /dev/null 2>&1; then
        echo "HEALTHY ✓"
    else
        echo "UNHEALTHY ✗"
    fi
else
    echo "Status: NOT FOUND"
fi
echo ""

# AWS Amplify
echo "--- AWS Amplify ---"
AMPLIFY_APP_ID=$(aws amplify list-apps \
    --query "apps[?name=='signalcopilot'].appId" \
    --output text 2>/dev/null || echo "")

if [ -n "$AMPLIFY_APP_ID" ]; then
    APP_DOMAIN=$(aws amplify get-app \
        --app-id "$AMPLIFY_APP_ID" \
        --query 'app.defaultDomain' \
        --output text)
    echo "Status: EXISTS"
    echo "App ID: $AMPLIFY_APP_ID"
    echo "URL: https://master.$APP_DOMAIN"
else
    echo "Status: NOT FOUND"
fi
echo ""

# IAM Role
echo "--- IAM Role ---"
if aws iam get-role --role-name AppRunnerECRAccessRole &>/dev/null; then
    ROLE_ARN=$(aws iam get-role \
        --role-name AppRunnerECRAccessRole \
        --query 'Role.Arn' \
        --output text)
    echo "Status: EXISTS"
    echo "ARN: $ROLE_ARN"
else
    echo "Status: NOT FOUND"
fi

echo ""
echo "========================================"
