# Vercel Frontend Deployment Guide

## Pre-requisites

- ✅ Backend deployed to Render: https://marketsignal-5qgv.onrender.com
- ✅ Database connected and migrations applied
- ✅ GitHub repository: https://github.com/YOUR_USERNAME/marketsignal

## Step 1: Deploy to Vercel

### Option A: Deploy via Vercel Dashboard (Recommended)

1. Go to https://vercel.com/new
2. Sign in with GitHub
3. Click "Import Project"
4. Search for or paste your repository URL
5. Select the `marketsignal` repository

### Option B: Deploy via Vercel CLI

```bash
# Install Vercel CLI (if not installed)
npm install -g vercel

# Navigate to project root
cd /Users/ajin/Documents/GitHub/marketsignal

# Deploy
vercel --prod
```

## Step 2: Configure Build Settings in Vercel

When prompted or in the Vercel dashboard settings:

- **Framework Preset**: Next.js
- **Root Directory**: `frontend` (IMPORTANT!)
- **Build Command**: `npm run build`
- **Output Directory**: `.next`
- **Install Command**: `npm install`

## Step 3: Add Environment Variables in Vercel

In your Vercel project settings:

1. Go to **Settings** → **Environment Variables**
2. Add the following variable:

| Name                  | Value | Environment |
| --------------------- | ----- | ----------- |
| `NEXT_PUBLIC_API_URL` | `     |

` | Production, Preview, Development |

**Important Notes:**

- The variable MUST start with `NEXT_PUBLIC_` to be accessible in the browser
- No trailing slash in the URL
- Apply to all environments (Production, Preview, Development)

## Step 4: Deploy

1. If using dashboard: Click "Deploy"
2. If using CLI: The deployment happens automatically

Wait for the build to complete (usually 1-2 minutes).

## Step 5: Get Your Vercel URL

After deployment completes, Vercel will provide:

- **Production URL**: `https://your-project-name.vercel.app`
- You may also set up a custom domain later

## Step 6: Update Backend CORS Settings

Once you have your Vercel URL, update the backend CORS configuration:

1. Go to Render dashboard
2. Navigate to your `marketsignal` service
3. Go to Environment Variables
4. Update `AllowedOrigins` to include your Vercel URL:
   ```
   AllowedOrigins=https://your-project-name.vercel.app,http://localhost:3000
   ```
5. Save changes (Render will redeploy automatically)

## Troubleshooting

### Build Fails

- Ensure Root Directory is set to `frontend`
- Check that `NEXT_PUBLIC_API_URL` is set correctly
- Review build logs in Vercel dashboard

### API Calls Fail

- Verify `NEXT_PUBLIC_API_URL` has no trailing slash
- Check browser console for CORS errors
- Ensure backend CORS includes your Vercel URL

### Page Not Found

- Ensure Output Directory is `.next` (not `frontend/.next`)
- Verify Root Directory is `frontend`

## Expected Result

After successful deployment:

- Frontend accessible at: `https://your-project-name.vercel.app`
- Backend API at: `https://marketsignal-5qgv.onrender.com`
- Frontend can communicate with backend
- CORS properly configured

## Next Steps

1. ✅ Deploy frontend to Vercel
2. ⏭️ Update backend CORS with Vercel URL
3. ⏭️ Test complete user flow
4. ⏭️ (Optional) Set up custom domain
5. ⏭️ (Optional) Configure analytics and monitoring
