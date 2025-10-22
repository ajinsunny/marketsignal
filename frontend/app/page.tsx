'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { api } from '@/lib/api';
import dynamic from 'next/dynamic';

const Silk = dynamic(() => import('@/components/Silk'), { ssr: false });

export default function Home() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);

  // Prefetch dashboard for instant navigation
  useEffect(() => {
    router.prefetch('/dashboard');
  }, [router]);

  const handleJumpIn = async () => {
    setLoading(true);

    // Start auth in background while navigating
    const authPromise = api.jumpIn();

    // Navigate immediately for perceived performance
    router.push('/dashboard');

    // Ensure auth completes
    try {
      await authPromise;
    } catch (error) {
      console.error('Failed to authenticate:', error);
      // Dashboard will redirect if auth failed
    }
  };

  return (
    <div className="relative min-h-screen flex items-center justify-center p-4 overflow-hidden">
      {/* Silk Background */}
      <div className="absolute inset-0 z-0">
        <Silk color="#29ff9b" speed={5} scale={1} noiseIntensity={1.5} rotation={0} />
      </div>

      {/* Content */}
      <div className="relative z-10 max-w-4xl mx-auto text-center">
        <div className="mb-8">
          <h1 className="text-6xl font-bold text-white mb-4 drop-shadow-lg">
            Market Signal
          </h1>
          <p className="text-xl text-white/90 mb-2 drop-shadow">
            Financial News → Personalized Impact Alerts
          </p>
          <p className="text-sm text-white/70 drop-shadow">
            Filter market noise. Focus on what matters to your portfolio.
          </p>
        </div>

        <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8 mb-8 border border-white/20 shadow-2xl">
          <button
            onClick={handleJumpIn}
            disabled={loading}
            className="bg-blue-500 hover:bg-blue-600 text-white font-bold text-xl py-4 px-12 rounded-full shadow-lg hover:shadow-xl transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {loading ? 'Starting...' : 'Jump In'}
          </button>
        </div>

        <div className="text-xs text-white/60 drop-shadow">
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
