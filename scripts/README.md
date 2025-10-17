# AWS Deployment Scripts

Automation scripts for deploying and managing Signal Copilot on AWS.

## Prerequisites

- AWS CLI configured (`aws configure`)
- Docker Desktop installed and running
- .NET 8 SDK installed
- Appropriate AWS IAM permissions

## Scripts

### 1. `deploy-aws.sh` - Full Deployment Automation

Automates the entire AWS deployment process.

```bash
./scripts/deploy-aws.sh
```

**What it does:**
- ✅ Generates secure database password and JWT secret
- ✅ Creates RDS PostgreSQL database (db.t4g.micro)
- ✅ Creates ECR repository
- ✅ Builds and pushes Docker image
- ✅ Creates IAM roles for App Runner
- ✅ Deploys backend to AWS App Runner
- ✅ Tests backend health endpoint
- ✅ Saves all deployment information to timestamped file

**Duration:** ~15-20 minutes

**Output:**
- Creates `aws-deployment-info-TIMESTAMP.txt` with all credentials and URLs
- **IMPORTANT:** Keep this file secure!

**Note:** You still need to:
1. Run database migrations (see AWS_DEPLOYMENT.md)
2. Deploy frontend to Amplify (see AWS_CONSOLE_DEPLOYMENT.md)
3. Update CORS with Amplify URL (use `update-cors.sh`)

### 2. `check-status.sh` - Check Deployment Status

Checks the status of all AWS resources.

```bash
./scripts/check-status.sh
```

**Output:**
- RDS database status and endpoint
- ECR repository and image count
- App Runner service status and URL
- App Runner health check result
- Amplify app status and URL
- IAM role status

**Use case:** Quick health check of your deployment

### 3. `update-cors.sh` - Update CORS Settings

Updates App Runner CORS to allow your frontend domain.

```bash
./scripts/update-cors.sh <AMPLIFY_URL>
```

**Example:**
```bash
./scripts/update-cors.sh https://master.abc123.amplifyapp.com
```

**When to use:**
- After deploying frontend to Amplify
- When changing frontend domain
- When testing from different domains

**Note:** Service will automatically redeploy (~3 minutes)

### 4. `teardown-aws.sh` - Delete All AWS Resources

**⚠️ WARNING: This permanently deletes ALL data!**

```bash
./scripts/teardown-aws.sh
```

**What it deletes:**
- AWS Amplify app
- AWS App Runner service
- Amazon RDS database (ALL DATA LOST!)
- Amazon ECR repository and images
- IAM roles

**Safety:**
- Requires typing "DELETE" to confirm
- No way to recover deleted data
- Use only when completely done with deployment

**Use case:**
- Cleaning up test deployments
- Starting fresh deployment
- Removing project completely

## Deployment Workflow

### Initial Deployment

```bash
# 1. Deploy backend infrastructure
./scripts/deploy-aws.sh

# 2. Deploy frontend (use AWS Console - see AWS_CONSOLE_DEPLOYMENT.md)
#    Or follow AWS_DEPLOYMENT.md for manual steps

# 3. Update CORS with your Amplify URL
./scripts/update-cors.sh https://master.YOUR_AMPLIFY_ID.amplifyapp.com

# 4. Check everything is working
./scripts/check-status.sh
```

### Updates & Redeployments

```bash
# Check current status
./scripts/check-status.sh

# If you update backend code, rebuild and push:
cd /path/to/marketsignal
docker build -f Dockerfile.root -t signalcopilot-api:latest .
aws ecr get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin 107792665903.dkr.ecr.us-east-1.amazonaws.com/signalcopilot-api
docker tag signalcopilot-api:latest 107792665903.dkr.ecr.us-east-1.amazonaws.com/signalcopilot-api:latest
docker push 107792665903.dkr.ecr.us-east-1.amazonaws.com/signalcopilot-api:latest

# Then manually redeploy in App Runner Console
```

### Cleanup

```bash
# Delete all resources (type DELETE to confirm)
./scripts/teardown-aws.sh
```

## Troubleshooting

### Permission Errors

If you get permission errors:
- Check AWS CLI is configured: `aws sts get-caller-identity`
- Ensure your IAM user has necessary permissions
- See AWS_CONSOLE_DEPLOYMENT.md for console-based deployment

### Docker Build Fails

```bash
# Make sure you're in the project root
cd /path/to/marketsignal

# Check Dockerfile exists
ls -l Dockerfile.root

# Try building locally first
docker build -f Dockerfile.root -t signalcopilot-api:latest .
```

### App Runner Deployment Fails

```bash
# Check App Runner logs
aws logs tail /aws/apprunner/signalcopilot-api/service --follow

# Check service status
aws apprunner describe-service \
  --service-arn $(aws apprunner list-services \
    --query "ServiceSummaryList[?ServiceName=='signalcopilot-api'].ServiceArn" \
    --output text)
```

### Database Connection Issues

```bash
# Check database status
aws rds describe-db-instances --db-instance-identifier signalcopilot-db

# Check security group allows App Runner
# Go to AWS Console → RDS → signalcopilot-db → Connectivity & security
```

## Environment Variables

Scripts use these variables (set automatically):
- `AWS_REGION` - AWS region (default: us-east-1)
- `AWS_ACCOUNT_ID` - Your AWS account ID (from `aws sts get-caller-identity`)
- `DB_PASSWORD` - Auto-generated secure password
- `JWT_SECRET` - Auto-generated JWT signing key

## Security Notes

1. **Keep deployment info files secure** - They contain database passwords
2. **Don't commit `aws-deployment-info-*.txt` to git** - Already in .gitignore
3. **Use AWS Secrets Manager for production** - See AWS_DEPLOYMENT.md
4. **Rotate credentials regularly** - Especially for production
5. **Enable MFA on AWS account** - Always recommended

## Manual Deployment

If scripts don't work due to permissions:
- See `AWS_CONSOLE_DEPLOYMENT.md` for step-by-step console guide
- See `AWS_DEPLOYMENT.md` for comprehensive manual CLI guide
- See `AWS_QUICKSTART.md` for quick CLI deployment

## Support

- AWS Support: https://console.aws.amazon.com/support
- Project Documentation: See `AWS_DEPLOYMENT.md`
- Issues: GitHub Issues

## Cost Estimates

Running these scripts will create AWS resources with costs:
- RDS db.t4g.micro: ~$15/month (free tier: 750 hours/month for 12 months)
- App Runner: ~$10-20/month (pay per use)
- ECR: ~$0.10/GB/month
- Amplify: $0-5/month (free tier: 1000 build minutes)

**Estimated total: ~$25-40/month** (or ~$5-10/month with free tier)

Remember to run `teardown-aws.sh` when done testing to avoid charges!
