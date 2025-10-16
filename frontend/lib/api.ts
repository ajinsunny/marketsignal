const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5076';

export interface User {
  email: string;
  token: string;
}

export type RiskProfile = 'Conservative' | 'Balanced' | 'Aggressive';
export type HoldingIntent = 'Trade' | 'Accumulate' | 'Income' | 'Hold';
export type SourceTier = 'Unknown' | 'Premium' | 'Standard' | 'Social' | 'Official';

export interface UserProfile {
  riskProfile: RiskProfile;
  cashBuffer?: number;
  createdAt: string;
}

export interface Holding {
  id: number;
  ticker: string;
  shares: number;
  costBasis: number;
  acquiredAt?: string;
  intent: HoldingIntent;
  addedAt: string;
}

export interface Impact {
  id: number;
  impactScore: number;
  exposure: number;
  computedAt: string;
  article: {
    id: number;
    ticker: string;
    headline: string;
    summary?: string;
    sourceUrl?: string;
    publisher?: string;
    publishedAt: string;
    sourceTier?: SourceTier;
  };
  holding: {
    id: number;
    ticker: string;
    shares: number;
  };
}

export interface ImpactsResponse {
  impacts: Impact[];
  pagination: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  };
}

export interface AnalogData {
  count: number;
  pattern: string;
  medianMove5D?: number;
  medianMove30D?: number;
}

// PHASE 4B: Portfolio Analytics Types
export interface PortfolioMetrics {
  totalValue: number;
  concentrationIndex: number;
  largestPosition?: { Item1: string; Item2: number };
  topConcentrations: Array<{ Item1: string; Item2: number }>;
}

export interface IntentMetrics {
  intent: HoldingIntent;
  count: number;
  totalValue: number;
  averageExposure: number;
  averageHoldingPeriodDays: number;
}

export interface HoldingPerformance {
  holdingId: number;
  ticker: string;
  holdingPeriodDays: number;
  totalImpactScore: number;
  positiveImpactsCount: number;
  negativeImpactsCount: number;
  intent: HoldingIntent;
}

export interface RebalanceRecommendation {
  ticker: string;
  action: 'StrongBuy' | 'Buy' | 'Hold' | 'Sell' | 'StrongSell';
  suggestion: string;
  confidenceScore: number;
  reasoning: string;
  keySignals: string[];
  sourceTier: string;
  averageImpactScore: number;
  newsCount: number;
  // PHASE 4A: Historical analogs for evidence-based recommendations
  analogs?: AnalogData | null;
}

export interface PortfolioSummary {
  overallAdvice: string;
  rationale: string;
  marketSentiment: string;
  keyActions: string[];
  riskAssessment: string;
}

export interface AnalysisResult {
  recommendations: RebalanceRecommendation[];
  analyzedAt: string;
  totalHoldings: number;
  impactsAnalyzed: number;
  summary: PortfolioSummary;
}

class ApiClient {
  private token: string | null = null;

  setToken(token: string) {
    this.token = token;
    if (typeof window !== 'undefined') {
      localStorage.setItem('token', token);
    }
  }

  getToken(): string | null {
    if (!this.token && typeof window !== 'undefined') {
      this.token = localStorage.getItem('token');
    }
    return this.token;
  }

  clearToken() {
    this.token = null;
    if (typeof window !== 'undefined') {
      localStorage.removeItem('token');
    }
  }

  private async fetch(endpoint: string, options: RequestInit = {}) {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string>),
    };

    const token = this.getToken();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${API_URL}${endpoint}`, {
      ...options,
      headers,
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP ${response.status}`);
    }

    const contentType = response.headers.get('content-type');
    if (contentType && contentType.includes('application/json')) {
      return await response.json();
    }
    return null;
  }

  // Auth
  async register(email: string, password: string) {
    return await this.fetch('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password, confirmPassword: password }),
    });
  }

  async login(email: string, password: string): Promise<User> {
    const response = await this.fetch('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
    this.setToken(response.token);
    return response;
  }

  // Auto-login for demo user
  async jumpIn(): Promise<User> {
    const demoEmail = `demo-${Date.now()}@signalcopilot.app`;
    const demoPassword = 'Demo123456';

    try {
      await this.register(demoEmail, demoPassword);
    } catch (e) {
      // User might already exist, continue to login
    }

    return await this.login(demoEmail, demoPassword);
  }

  // Profile
  async getProfile(): Promise<UserProfile> {
    return await this.fetch('/api/profile');
  }

  async updateProfile(riskProfile?: RiskProfile, cashBuffer?: number | null): Promise<UserProfile> {
    return await this.fetch('/api/profile', {
      method: 'PUT',
      body: JSON.stringify({
        riskProfile,
        cashBuffer,
        clearCashBuffer: cashBuffer === null,
      }),
    });
  }

  // Holdings
  async getHoldings(): Promise<Holding[]> {
    return await this.fetch('/api/holdings');
  }

  async addHolding(
    ticker: string,
    shares: number,
    costBasis: number,
    acquiredAt?: string,
    intent?: HoldingIntent
  ): Promise<Holding> {
    return await this.fetch('/api/holdings', {
      method: 'POST',
      body: JSON.stringify({ ticker, shares, costBasis, acquiredAt, intent }),
    });
  }

  async updateHolding(
    id: number,
    shares: number,
    costBasis: number,
    acquiredAt?: string,
    intent?: HoldingIntent
  ): Promise<Holding> {
    return await this.fetch(`/api/holdings/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ shares, costBasis, acquiredAt, intent }),
    });
  }

  async deleteHolding(id: number): Promise<void> {
    await this.fetch(`/api/holdings/${id}`, {
      method: 'DELETE',
    });
  }

  // Portfolio image upload
  async uploadPortfolioImage(file: File): Promise<{ tickers: string[] }> {
    const formData = new FormData();
    formData.append('image', file);

    const token = this.getToken();
    const headers: Record<string, string> = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${API_URL}/api/portfolio/upload-image`, {
      method: 'POST',
      headers,
      body: formData,
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP ${response.status}`);
    }

    return await response.json();
  }

  // Impacts
  async getImpacts(page: number = 1, pageSize: number = 20): Promise<ImpactsResponse> {
    return await this.fetch(`/api/impacts?page=${page}&pageSize=${pageSize}`);
  }

  async getHighImpacts(): Promise<Impact[]> {
    return await this.fetch('/api/impacts/high-impact');
  }

  // Jobs
  async triggerNewsFetch(): Promise<{ jobId: string }> {
    return await this.fetch('/api/jobs/fetch-news', {
      method: 'POST',
    });
  }

  async triggerAlerts(): Promise<{ jobId: string }> {
    return await this.fetch('/api/jobs/generate-alerts', {
      method: 'POST',
    });
  }

  async triggerDigests(): Promise<{ jobId: string }> {
    return await this.fetch('/api/jobs/generate-digests', {
      method: 'POST',
    });
  }

  // Analysis
  async getRebalanceSuggestions(): Promise<AnalysisResult> {
    return await this.fetch('/api/analysis/rebalance-suggestions');
  }

  // PHASE 4B: Portfolio Analytics
  async getPortfolioMetrics(): Promise<PortfolioMetrics> {
    return await this.fetch('/api/portfolio/metrics');
  }

  async getIntentMetrics(): Promise<Record<HoldingIntent, IntentMetrics>> {
    return await this.fetch('/api/portfolio/intent-metrics');
  }

  async getHoldingPerformance(holdingId: number): Promise<HoldingPerformance> {
    return await this.fetch(`/api/portfolio/holding-performance/${holdingId}`);
  }
}

export const api = new ApiClient();
