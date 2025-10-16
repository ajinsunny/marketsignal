'use client';

import { PortfolioMetrics, IntentMetrics, HoldingIntent, RiskProfile } from '@/lib/api';

/**
 * PHASE 4B: PortfolioContext Component
 *
 * Displays comprehensive portfolio analytics including:
 * - Risk profile and cash buffer
 * - Concentration metrics (HHI index)
 * - Intent-based allocation breakdown
 * - Top positions
 *
 * Purpose: Give users full transparency into their portfolio composition
 * and how it aligns with their investment strategy.
 */

interface PortfolioContextProps {
  metrics: PortfolioMetrics;
  intentMetrics: Record<HoldingIntent, IntentMetrics>;
  riskProfile?: RiskProfile;
  cashBuffer?: number;
}

export default function PortfolioContext({
  metrics,
  intentMetrics,
  riskProfile,
  cashBuffer,
}: PortfolioContextProps) {
  // Helper to get concentration level description
  const getConcentrationLevel = (hhi: number): { label: string; color: string; description: string } => {
    if (hhi < 1500) {
      return {
        label: 'Diversified',
        color: 'text-green-700 bg-green-50 border-green-200',
        description: 'Well-diversified across multiple holdings'
      };
    } else if (hhi < 2500) {
      return {
        label: 'Moderate',
        color: 'text-yellow-700 bg-yellow-50 border-yellow-200',
        description: 'Moderate concentration - monitor largest positions'
      };
    } else {
      return {
        label: 'Concentrated',
        color: 'text-red-700 bg-red-50 border-red-200',
        description: 'High concentration - consider diversifying'
      };
    }
  };

  const concentrationLevel = getConcentrationLevel(metrics.concentrationIndex);

  // Get intent emoji
  const getIntentEmoji = (intent: HoldingIntent): string => {
    switch (intent) {
      case 'Trade': return '🎯';
      case 'Accumulate': return '📈';
      case 'Income': return '💰';
      case 'Hold': return '🔒';
      default: return '📊';
    }
  };

  // Get risk profile emoji
  const getRiskEmoji = (profile?: RiskProfile): string => {
    switch (profile) {
      case 'Conservative': return '🛡️';
      case 'Aggressive': return '🚀';
      case 'Balanced': return '⚖️';
      default: return '📊';
    }
  };

  // Get active intents (with holdings)
  const activeIntents = Object.values(intentMetrics).filter(m => m.count > 0);

  return (
    <div className="bg-white rounded-lg shadow-lg p-6 space-y-6">
      <div className="flex items-center justify-between border-b border-gray-200 pb-4">
        <h2 className="text-xl font-bold text-gray-900">Portfolio Overview</h2>
        <div className="text-2xl font-bold text-blue-600">
          ${metrics.totalValue.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
        </div>
      </div>

      {/* User Profile Section */}
      {riskProfile && (
        <div className="grid grid-cols-2 gap-4">
          <div className="p-3 bg-blue-50 rounded-lg border border-blue-200">
            <div className="text-xs text-blue-600 font-semibold mb-1">Investment Style</div>
            <div className="flex items-center gap-2">
              <span className="text-2xl">{getRiskEmoji(riskProfile)}</span>
              <span className="text-sm font-bold text-blue-900">{riskProfile}</span>
            </div>
          </div>

          {cashBuffer != null && (
            <div className="p-3 bg-green-50 rounded-lg border border-green-200">
              <div className="text-xs text-green-600 font-semibold mb-1">Cash Buffer</div>
              <div className="text-sm font-bold text-green-900">
                ${cashBuffer.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Concentration Metrics */}
      <div className="space-y-3">
        <h3 className="text-sm font-semibold text-gray-700">Concentration Analysis</h3>

        <div className={`p-3 rounded-lg border ${concentrationLevel.color}`}>
          <div className="flex items-center justify-between mb-2">
            <span className="text-sm font-semibold">{concentrationLevel.label}</span>
            <span className="text-xs font-mono">HHI: {metrics.concentrationIndex.toFixed(0)}</span>
          </div>
          <div className="text-xs">{concentrationLevel.description}</div>
        </div>

        {/* Largest Position */}
        {metrics.largestPosition && (
          <div className="p-3 bg-gray-50 rounded-lg border border-gray-200">
            <div className="text-xs text-gray-600 mb-1">Largest Position</div>
            <div className="flex items-center justify-between">
              <span className="font-bold text-gray-900">{metrics.largestPosition.ticker}</span>
              <span className="text-sm font-semibold text-gray-700">
                {(metrics.largestPosition.exposurePct * 100).toFixed(1)}%
              </span>
            </div>
          </div>
        )}

        {/* Top 3 Positions */}
        {metrics.topConcentrations.length > 0 && (
          <div className="space-y-2">
            <div className="text-xs font-semibold text-gray-600">Top Holdings</div>
            {metrics.topConcentrations.map((pos, idx) => (
              <div key={idx} className="flex items-center justify-between text-sm">
                <span className="text-gray-700">{pos.ticker}</span>
                <div className="flex items-center gap-2">
                  <div className="w-24 h-2 bg-gray-200 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-blue-500"
                      style={{ width: `${Math.min(pos.exposurePct * 100, 100)}%` }}
                    />
                  </div>
                  <span className="text-xs font-semibold text-gray-600 w-12 text-right">
                    {(pos.exposurePct * 100).toFixed(1)}%
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Intent-Based Allocation */}
      {activeIntents.length > 0 && (
        <div className="space-y-3">
          <h3 className="text-sm font-semibold text-gray-700">Strategy Allocation</h3>

          <div className="space-y-2">
            {activeIntents.map((intent) => (
              <div key={intent.intent} className="p-3 bg-gray-50 rounded-lg border border-gray-200">
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <span className="text-xl">{getIntentEmoji(intent.intent)}</span>
                    <span className="text-sm font-semibold text-gray-900">{intent.intent}</span>
                  </div>
                  <span className="text-xs font-semibold text-gray-600">
                    {intent.count} {intent.count === 1 ? 'holding' : 'holdings'}
                  </span>
                </div>

                <div className="grid grid-cols-2 gap-2 text-xs">
                  <div>
                    <div className="text-gray-500">Value</div>
                    <div className="font-semibold text-gray-900">
                      ${intent.totalValue.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}
                    </div>
                  </div>
                  <div>
                    <div className="text-gray-500">Avg Period</div>
                    <div className="font-semibold text-gray-900">
                      {intent.averageHoldingPeriodDays} days
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
