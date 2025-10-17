#!/bin/bash
# AWS Teardown Script - Deletes all Signal Copilot AWS resources
# WARNING: This will permanently delete all data!

set -e

RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${RED}========================================${NC}"
echo -e "${RED}   AWS Resource Teardown${NC}"
echo -e "${RED}   THIS WILL DELETE ALL DATA!${NC}"
echo -e "${RED}========================================${NC}"
echo ""
echo -e "${YELLOW}This script will delete:${NC}"
echo "  - AWS Amplify app (signalcopilot)"
echo "  - AWS App Runner service (signalcopilot-api)"
echo "  - Amazon RDS database (signalcopilot-db) - ALL DATA WILL BE LOST!"
echo "  - Amazon ECR repository (signalcopilot-api)"
echo "  - IAM roles (AppRunnerECRAccessRole)"
echo ""
echo -e "${RED}Are you absolutely sure? Type 'DELETE' to confirm:${NC}"
read -r confirmation

if [ "$confirmation" != "DELETE" ]; then
    echo "Teardown cancelled."
    exit 0
fi

AWS_REGION=$(aws configure get region || echo "us-east-1")

echo ""
echo "Starting teardown in region: $AWS_REGION"
echo ""

# Delete Amplify app
echo "Deleting AWS Amplify app..."
AMPLIFY_APP_ID=$(aws amplify list-apps \
    --query "apps[?name=='signalcopilot'].appId" \
    --output text 2>/dev/null || echo "")

if [ -n "$AMPLIFY_APP_ID" ]; then
    aws amplify delete-app --app-id "$AMPLIFY_APP_ID" || echo "Failed to delete Amplify app"
    echo "✓ Amplify app deleted"
else
    echo "⊘ No Amplify app found"
fi

# Delete App Runner service
echo "Deleting AWS App Runner service..."
SERVICE_ARN=$(aws apprunner list-services \
    --query "ServiceSummaryList[?ServiceName=='signalcopilot-api'].ServiceArn" \
    --output text 2>/dev/null || echo "")

if [ -n "$SERVICE_ARN" ]; then
    aws apprunner delete-service --service-arn "$SERVICE_ARN" || echo "Failed to delete App Runner service"
    echo "✓ App Runner service deleted"
else
    echo "⊘ No App Runner service found"
fi

# Delete RDS database
echo "Deleting RDS database (this may take a few minutes)..."
if aws rds describe-db-instances --db-instance-identifier signalcopilot-db &>/dev/null; then
    aws rds delete-db-instance \
        --db-instance-identifier signalcopilot-db \
        --skip-final-snapshot || echo "Failed to delete RDS database"
    echo "✓ RDS database deletion initiated"
else
    echo "⊘ No RDS database found"
fi

# Delete ECR repository
echo "Deleting ECR repository..."
if aws ecr describe-repositories --repository-names signalcopilot-api &>/dev/null; then
    aws ecr delete-repository \
        --repository-name signalcopilot-api \
        --force || echo "Failed to delete ECR repository"
    echo "✓ ECR repository deleted"
else
    echo "⊘ No ECR repository found"
fi

# Detach and delete IAM role
echo "Deleting IAM role..."
if aws iam get-role --role-name AppRunnerECRAccessRole &>/dev/null; then
    aws iam detach-role-policy \
        --role-name AppRunnerECRAccessRole \
        --policy-arn arn:aws:iam::aws:policy/service-role/AWSAppRunnerServicePolicyForECRAccess 2>/dev/null || true

    aws iam delete-role --role-name AppRunnerECRAccessRole || echo "Failed to delete IAM role"
    echo "✓ IAM role deleted"
else
    echo "⊘ No IAM role found"
fi

echo ""
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}   Teardown Complete${NC}"
echo -e "${YELLOW}========================================${NC}"
echo ""
echo "All Signal Copilot AWS resources have been deleted."
echo "Note: RDS database deletion may take several minutes to complete."
echo ""
