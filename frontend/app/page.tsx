'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { api } from '@/lib/api';

export default function Home() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);

  const handleJumpIn = async () => {
    setLoading(true);
    try {
      await api.jumpIn();
      router.push('/dashboard');
    } catch (error) {
      console.error('Failed to jump in:', error);
      alert('Failed to start. Please try again.');
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-blue-900 to-slate-900 flex items-center justify-center p-4">
      <div className="max-w-4xl mx-auto text-center">
        <div className="mb-8">
          <h1 className="text-6xl font-bold text-white mb-4">
            Signal Copilot
          </h1>
          <p className="text-xl text-blue-200 mb-2">
            Financial News →  Personalized Impact Alerts
          </p>
          <p className="text-sm text-blue-300/70">
            Filter market noise. Focus on what matters to your portfolio.
          </p>
        </div>

        <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8 mb-8 border border-white/20">
          <div className="grid md:grid-cols-3 gap-6 mb-8">
            <div className="text-center">
              <div className="text-4xl mb-2">📰</div>
              <h3 className="text-lg font-semibold text-white mb-1">News Ingestion</h3>
              <p className="text-sm text-blue-200">Real-time financial headlines for your holdings</p>
            </div>
            <div className="text-center">
              <div className="text-4xl mb-2">🎯</div>
              <h3 className="text-lg font-semibold text-white mb-1">Impact Scoring</h3>
              <p className="text-sm text-blue-200">Personalized relevance based on your exposure</p>
            </div>
            <div className="text-center">
              <div className="text-4xl mb-2">🔔</div>
              <h3 className="text-lg font-semibold text-white mb-1">Smart Alerts</h3>
              <p className="text-sm text-blue-200">High-impact events + daily digests</p>
            </div>
          </div>

          <button
            onClick={handleJumpIn}
            disabled={loading}
            className="bg-blue-500 hover:bg-blue-600 text-white font-bold text-xl py-4 px-12 rounded-full shadow-lg hover:shadow-xl transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {loading ? 'Starting...' : 'Jump In'}
          </button>
        </div>

        <div className="text-xs text-blue-300/50">
          <p className="mb-2">
            Impact Score = direction × magnitude × confidence × exposure
          </p>
          <p>
            This is an awareness tool. Not financial advice.
          </p>
        </div>
      </div>
    </div>
  );
}
