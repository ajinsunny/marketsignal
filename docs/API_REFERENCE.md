# Signal Copilot API Reference

Complete API documentation for Signal Copilot – Lite backend endpoints.

**Base URL:** `https://localhost:5001` (development)

**Authentication:** JWT Bearer token (except Auth and Health endpoints)

**Content-Type:** `application/json`

---

## Table of Contents
1. [Authentication](#authentication)
2. [Holdings](#holdings)
3. [Impacts](#impacts)
4. [Portfolio](#portfolio)
5. [Profile](#profile)
6. [Analysis](#analysis)
7. [Jobs](#jobs)
8. [Health](#health)

---

## Authentication

### Register User
Create a new user account.

```http
POST /api/auth/register
```

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123"
}
```

**Validation Rules:**
- Email: Required, valid email format
- Password: Minimum 8 characters, requires digit, lowercase, uppercase

**Success Response (200 OK):**
```json
{
  "message": "User registered successfully"
}
```

**Error Response (400 Bad Request):**
```json
{
  "errors": {
    "Password": ["Password must be at least 8 characters"]
  }
}
```

---

### Login
Authenticate and receive JWT token.

```http
POST /api/auth/login
```

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123"
}
```

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiration": "2025-10-22T19:38:00Z"
}
```

**Error Response (401 Unauthorized):**
```json
{
  "error": "Invalid credentials"
}
```

**Usage:**
Include token in subsequent requests:
```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Holdings

All Holdings endpoints require authentication.

### List Holdings
Get all holdings for the authenticated user.

```http
GET /api/holdings
```

**Success Response (200 OK):**
```json
[
  {
    "id": 1,
    "ticker": "AAPL",
    "shares": 100.0,
    "costBasis": 150.00,
    "acquiredAt": "2024-01-15T00:00:00Z",
    "intent": "Hold",
    "addedAt": "2025-10-16T12:00:00Z",
    "updatedAt": null
  },
  {
    "id": 2,
    "ticker": "MSFT",
    "shares": 50.0,
    "costBasis": 300.00,
    "acquiredAt": "2024-06-01T00:00:00Z",
    "intent": "Accumulate",
    "addedAt": "2025-10-16T14:30:00Z",
    "updatedAt": "2025-10-17T10:00:00Z"
  }
]
```

---

### Get Holding by ID
Retrieve a specific holding.

```http
GET /api/holdings/{id}
```

**Path Parameters:**
- `id` (integer): Holding ID

**Success Response (200 OK):**
```json
{
  "id": 1,
  "ticker": "AAPL",
  "shares": 100.0,
  "costBasis": 150.00,
  "acquiredAt": "2024-01-15T00:00:00Z",
  "intent": "Hold",
  "addedAt": "2025-10-16T12:00:00Z",
  "updatedAt": null
}
```

**Error Response (404 Not Found):**
```json
{
  "error": "Holding not found"
}
```

---

### Create Holding
Add a new holding to the portfolio.

```http
POST /api/holdings
```

**Request Body:**
```json
{
  "ticker": "AAPL",
  "shares": 100.0,
  "costBasis": 150.00,
  "acquiredAt": "2024-01-15T00:00:00Z",
  "intent": "Hold"
}
```

**Field Constraints:**
- `ticker` (string, required): 1-10 uppercase characters
- `shares` (decimal, required): Greater than 0, up to 8 decimal places
- `costBasis` (decimal, optional): Greater than 0, 2 decimal places
- `acquiredAt` (datetime, optional): ISO 8601 format
- `intent` (string, optional): "Trade" | "Accumulate" | "Income" | "Hold"

**Success Response (201 Created):**
```json
{
  "id": 3,
  "ticker": "AAPL",
  "shares": 100.0,
  "costBasis": 150.00,
  "acquiredAt": "2024-01-15T00:00:00Z",
  "intent": "Hold",
  "addedAt": "2025-10-17T15:00:00Z",
  "updatedAt": null
}
```

**Error Response (400 Bad Request):**
```json
{
  "error": "You already have a holding for AAPL"
}
```

---

### Update Holding
Modify an existing holding.

```http
PUT /api/holdings/{id}
```

**Path Parameters:**
- `id` (integer): Holding ID

**Request Body:**
```json
{
  "shares": 150.0,
  "costBasis": 145.00,
  "acquiredAt": "2024-01-15T00:00:00Z",
  "intent": "Accumulate"
}
```

**Success Response (200 OK):**
```json
{
  "id": 1,
  "ticker": "AAPL",
  "shares": 150.0,
  "costBasis": 145.00,
  "acquiredAt": "2024-01-15T00:00:00Z",
  "intent": "Accumulate",
  "addedAt": "2025-10-16T12:00:00Z",
  "updatedAt": "2025-10-17T16:00:00Z"
}
```

**Error Response (404 Not Found):**
```json
{
  "error": "Holding not found"
}
```

---

### Delete Holding
Remove a holding from the portfolio.

```http
DELETE /api/holdings/{id}
```

**Path Parameters:**
- `id` (integer): Holding ID

**Success Response (200 OK):**
```json
{
  "message": "Holding deleted successfully"
}
```

**Error Response (404 Not Found):**
```json
{
  "error": "Holding not found"
}
```

---

## Impacts

### List Impacts
Get paginated impacts for the authenticated user.

```http
GET /api/impacts?page=1&pageSize=20&minImpactScore=0.5
```

**Query Parameters:**
- `page` (integer, optional): Page number (default: 1)
- `pageSize` (integer, optional): Items per page (default: 20, max: 100)
- `minImpactScore` (decimal, optional): Minimum absolute impact score filter

**Success Response (200 OK):**
```json
{
  "impacts": [
    {
      "id": 1,
      "impactScore": 0.8421,
      "exposure": 0.15,
      "computedAt": "2025-10-16T12:00:00Z",
      "article": {
        "id": 1,
        "ticker": "AAPL",
        "headline": "Apple beats Q3 estimates with strong iPhone sales",
        "summary": "Apple Inc. reported quarterly earnings...",
        "sourceUrl": "https://newsapi.org/...",
        "publisher": "Reuters",
        "publishedAt": "2025-10-16T10:00:00Z",
        "sourceTier": "Premium",
        "eventCategory": "EarningsBeatMiss"
      },
      "holding": {
        "id": 1,
        "ticker": "AAPL",
        "shares": 100.0,
        "intent": "Hold"
      },
      "signal": {
        "sentiment": 1,
        "magnitude": 2,
        "confidence": 0.85,
        "sourceCount": 3,
        "stanceAgreement": 0.95
      }
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 156,
    "totalPages": 8
  }
}
```

---

### Get High-Impact Events
Retrieve top 10 impacts above threshold for the user.

```http
GET /api/impacts/high-impact
```

**Success Response (200 OK):**
```json
{
  "impacts": [
    {
      "id": 5,
      "impactScore": 1.2045,
      "exposure": 0.25,
      "computedAt": "2025-10-17T08:00:00Z",
      "article": {
        "id": 12,
        "ticker": "TSLA",
        "headline": "Tesla announces major recall affecting 500,000 vehicles",
        "sourceTier": "Premium",
        "eventCategory": "ProductRecall"
      },
      "holding": {
        "id": 4,
        "ticker": "TSLA",
        "shares": 200.0
      },
      "signal": {
        "sentiment": -1,
        "magnitude": 3,
        "confidence": 0.92
      }
    }
  ]
}
```

---

## Portfolio

### Upload Portfolio Image
Extract tickers from a portfolio screenshot using Claude AI.

```http
POST /api/portfolio/upload-image
```

**Request:**
- Content-Type: `multipart/form-data`
- Field name: `file`
- Supported formats: JPEG, PNG, GIF, WebP
- Max file size: 10MB

**Example (curl):**
```bash
curl -X POST https://localhost:5001/api/portfolio/upload-image \
  -H "Authorization: Bearer <token>" \
  -F "file=@portfolio-screenshot.png"
```

**Success Response (200 OK):**
```json
{
  "tickers": ["AAPL", "MSFT", "GOOGL", "AMZN", "TSLA"]
}
```

**Error Response (400 Bad Request):**
```json
{
  "error": "File size exceeds 10MB limit"
}
```

---

### Get Portfolio Metrics
Retrieve concentration and allocation metrics.

```http
GET /api/portfolio/metrics
```

**Success Response (200 OK):**
```json
{
  "totalValue": 50000.00,
  "concentrationIndex": 1800,
  "largestPosition": {
    "ticker": "AAPL",
    "exposurePct": 25.0
  },
  "topConcentrations": [
    { "ticker": "AAPL", "exposurePct": 25.0 },
    { "ticker": "MSFT", "exposurePct": 20.0 },
    { "ticker": "GOOGL", "exposurePct": 15.0 },
    { "ticker": "AMZN", "exposurePct": 12.0 },
    { "ticker": "TSLA", "exposurePct": 10.0 }
  ]
}
```

**Concentration Index Interpretation:**
- < 1,500: Diversified
- 1,500 - 2,500: Moderate concentration
- > 2,500: Concentrated

---

### Get Intent Metrics
Retrieve allocation breakdown by holding intent.

```http
GET /api/portfolio/intent-metrics
```

**Success Response (200 OK):**
```json
{
  "Trade": {
    "count": 2,
    "totalValue": 5000.00,
    "averageExposure": 0.05,
    "averageHoldingPeriod": 15
  },
  "Accumulate": {
    "count": 3,
    "totalValue": 15000.00,
    "averageExposure": 0.15,
    "averageHoldingPeriod": 120
  },
  "Income": {
    "count": 1,
    "totalValue": 8000.00,
    "averageExposure": 0.08,
    "averageHoldingPeriod": 365
  },
  "Hold": {
    "count": 5,
    "totalValue": 22000.00,
    "averageExposure": 0.14,
    "averageHoldingPeriod": 540
  }
}
```

---

### Get Holding Performance
Retrieve performance metrics for a specific holding.

```http
GET /api/portfolio/holding-performance/{id}
```

**Path Parameters:**
- `id` (integer): Holding ID

**Success Response (200 OK):**
```json
{
  "ticker": "AAPL",
  "shares": 100.0,
  "costBasis": 150.00,
  "currentPrice": 175.50,
  "totalCost": 15000.00,
  "currentValue": 17550.00,
  "unrealizedGain": 2550.00,
  "unrealizedGainPct": 17.0,
  "holdingPeriodDays": 275
}
```

---

## Profile

### Get User Profile
Retrieve user preferences.

```http
GET /api/profile
```

**Success Response (200 OK):**
```json
{
  "riskProfile": "Balanced",
  "cashBuffer": 10000.00
}
```

---

### Update User Profile
Modify user preferences.

```http
PUT /api/profile
```

**Request Body:**
```json
{
  "riskProfile": "Aggressive",
  "cashBuffer": 15000.00
}
```

**Field Options:**
- `riskProfile` (string, optional): "Conservative" | "Balanced" | "Aggressive"
- `cashBuffer` (decimal, optional): Available cash (set to `null` to clear)
- `clearCashBuffer` (boolean, optional): Set to `true` to explicitly clear cash buffer

**Success Response (200 OK):**
```json
{
  "riskProfile": "Aggressive",
  "cashBuffer": 15000.00
}
```

---

## Analysis

### Get Rebalance Suggestions
Generate personalized portfolio recommendations.

```http
GET /api/analysis/rebalance-suggestions
```

**Success Response (200 OK):**
```json
{
  "summary": "Your portfolio shows moderate concentration with 3 high-impact events in the last 7 days.",
  "recommendations": [
    {
      "ticker": "AAPL",
      "exposure": 0.25,
      "aggregateImpact": 0.8421,
      "recommendation": "Consider trimming AAPL position (25% exposure). Historical patterns suggest -3.8% median/5d after earnings beats.",
      "analogs": [
        {
          "ticker": "AAPL",
          "eventCategory": "EarningsBeatMiss",
          "occurrenceCount": 12,
          "medianMove5D": -0.038,
          "medianMove30D": 0.052,
          "patternDescription": "−3.8% median/5d after earnings beats (12 occurrences)"
        }
      ]
    }
  ]
}
```

---

## Jobs

Manual triggers for background jobs (development/admin use).

### Trigger News Fetch
Immediately fetch news for all tracked tickers.

```http
POST /api/jobs/fetch-news
```

**Success Response (200 OK):**
```json
{
  "message": "News fetch job triggered successfully"
}
```

---

### Trigger High-Impact Alerts
Generate alerts for recent high-impact events.

```http
POST /api/jobs/generate-alerts
```

**Success Response (200 OK):**
```json
{
  "message": "High-impact alert generation triggered"
}
```

---

### Trigger Daily Digests
Generate daily digest emails for all users.

```http
POST /api/jobs/generate-digests
```

**Success Response (200 OK):**
```json
{
  "message": "Daily digest generation triggered"
}
```

---

### Get Job Status
Check status of background job system.

```http
GET /api/jobs/status
```

**Success Response (200 OK):**
```json
{
  "hangfireRunning": true,
  "recurringJobs": [
    {
      "id": "fetch-news",
      "cron": "*/30 * * * *",
      "lastExecution": "2025-10-17T16:30:00Z",
      "nextExecution": "2025-10-17T17:00:00Z"
    },
    {
      "id": "daily-digest",
      "cron": "0 9 * * *",
      "lastExecution": "2025-10-17T09:00:00Z",
      "nextExecution": "2025-10-18T09:00:00Z"
    }
  ]
}
```

---

## Health

Public endpoints for health monitoring (no authentication required).

### Basic Health Check
Simple liveness check.

```http
GET /api/health
```

**Success Response (200 OK):**
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-17T16:45:00Z"
}
```

---

### Detailed Health Check
Comprehensive system status.

```http
GET /api/health/detailed
```

**Success Response (200 OK):**
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-17T16:45:00Z",
  "version": "0.4.0",
  "uptime": "2d 4h 23m",
  "database": {
    "status": "Connected",
    "responseTime": "12ms"
  },
  "services": {
    "hangfire": "Running",
    "newsProviders": {
      "NewsApi": "Disabled",
      "Finnhub": "Disabled",
      "SecEdgar": "Enabled"
    }
  }
}
```

**Error Response (503 Service Unavailable):**
```json
{
  "status": "Unhealthy",
  "timestamp": "2025-10-17T16:45:00Z",
  "database": {
    "status": "Disconnected",
    "error": "Connection timeout"
  }
}
```

---

## Error Handling

### Standard Error Response Format

```json
{
  "error": "Brief error message",
  "details": "Detailed explanation (optional)",
  "statusCode": 400
}
```

### HTTP Status Codes

| Code | Meaning | When Used |
|------|---------|-----------|
| 200 | OK | Successful request |
| 201 | Created | Resource created successfully |
| 400 | Bad Request | Validation error, invalid input |
| 401 | Unauthorized | Missing or invalid JWT token |
| 403 | Forbidden | Valid token but insufficient permissions |
| 404 | Not Found | Resource doesn't exist |
| 409 | Conflict | Duplicate resource (e.g., ticker already held) |
| 500 | Internal Server Error | Unexpected server error |
| 503 | Service Unavailable | Service temporarily unavailable |

---

## Rate Limiting

Currently no rate limiting implemented. Planned for future releases.

---

## Versioning

API version is included in the response headers:

```
X-API-Version: 0.4.0
```

Future breaking changes will use URL versioning:
```
/api/v2/holdings
```

---

## SDK Examples

### TypeScript/JavaScript (Frontend)

```typescript
import api from './lib/api';

// Login
const { token } = await api.login('user@example.com', 'password');

// Get holdings
const holdings = await api.getHoldings();

// Add holding
const newHolding = await api.addHolding({
  ticker: 'AAPL',
  shares: 100,
  costBasis: 150.00,
  intent: 'Hold'
});

// Get impacts with pagination
const { impacts, pagination } = await api.getImpacts(1, 20);

// Upload portfolio image
const file = document.querySelector('#file-input').files[0];
const { tickers } = await api.uploadPortfolioImage(file);
```

### cURL Examples

```bash
# Login
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"SecurePass123"}'

# Get holdings
curl -X GET https://localhost:5001/api/holdings \
  -H "Authorization: Bearer <token>"

# Add holding
curl -X POST https://localhost:5001/api/holdings \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"ticker":"AAPL","shares":100,"costBasis":150.00,"intent":"Hold"}'

# Upload image
curl -X POST https://localhost:5001/api/portfolio/upload-image \
  -H "Authorization: Bearer <token>" \
  -F "file=@portfolio.png"
```

---

## Swagger Documentation

Interactive API documentation available at:

**Development:** `https://localhost:5001/swagger`

Swagger UI provides:
- All endpoints with descriptions
- Request/response schemas
- Try-it-out functionality
- Authentication testing

---

**Last Updated:** October 21, 2025
**API Version:** 0.4.0
