const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5076';

export interface User {
  email: string;
  token: string;
}

export interface Holding {
  id: number;
  ticker: string;
  shares: number;
  costBasis: number;
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
}

export interface AnalysisResult {
  recommendations: RebalanceRecommendation[];
  analyzedAt: string;
  totalHoldings: number;
  impactsAnalyzed: number;
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
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
      ...options.headers,
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

  // Holdings
  async getHoldings(): Promise<Holding[]> {
    return await this.fetch('/api/holdings');
  }

  async addHolding(ticker: string, shares: number, costBasis: number): Promise<Holding> {
    return await this.fetch('/api/holdings', {
      method: 'POST',
      body: JSON.stringify({ ticker, shares, costBasis }),
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
    const headers: HeadersInit = {};
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
}

export const api = new ApiClient();
