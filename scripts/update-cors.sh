#!/bin/bash
# Update CORS settings in App Runner

set -e

if [ -z "$1" ]; then
    echo "Usage: ./update-cors.sh <AMPLIFY_URL>"
    echo "Example: ./update-cors.sh https://master.abc123.amplifyapp.com"
    exit 1
fi

AMPLIFY_URL=$1
AWS_REGION=$(aws configure get region || echo "us-east-1")

echo "Updating CORS for App Runner service..."

SERVICE_ARN=$(aws apprunner list-services \
    --query "ServiceSummaryList[?ServiceName=='signalcopilot-api'].ServiceArn" \
    --output text)

if [ -z "$SERVICE_ARN" ]; then
    echo "Error: App Runner service 'signalcopilot-api' not found"
    exit 1
fi

echo "Service ARN: $SERVICE_ARN"
echo "Setting AllowedOrigins to: $AMPLIFY_URL"

aws apprunner update-service \
    --service-arn "$SERVICE_ARN" \
    --source-configuration "{
        \"ImageRepository\": {
            \"ImageConfiguration\": {
                \"RuntimeEnvironmentVariables\": {
                    \"AllowedOrigins\": \"$AMPLIFY_URL\"
                }
            }
        }
    }" \
    --region "$AWS_REGION"

echo "✓ CORS updated successfully"
echo "Note: Service will redeploy automatically (takes ~3 minutes)"
