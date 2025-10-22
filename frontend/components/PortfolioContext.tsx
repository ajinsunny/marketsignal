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
    <div className="space-y-4">
      <div className="flex items-center justify-between pb-3 border-b border-gray-200">
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

        {/* Top Holdings Pie Chart */}
        {metrics.topConcentrations.length > 0 && (
          <div className="space-y-3">
            <div className="text-xs font-semibold text-gray-600">Top Holdings</div>

            {/* Pie Chart */}
            <div className="flex items-center justify-center">
              <svg width="180" height="180" viewBox="0 0 180 180" className="transform -rotate-90">
                {(() => {
                  const colors = ['#3B82F6', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6'];
                  let cumulativePercent = 0;

                  // Calculate total percent from top holdings
                  const topHoldingsPercent = metrics.topConcentrations.reduce((sum, pos) => sum + pos.exposurePct, 0);
                  const otherPercent = 1 - topHoldingsPercent;

                  const slices = [];

                  // Render top holdings
                  metrics.topConcentrations.forEach((pos, idx) => {
                    const percent = pos.exposurePct;
                    const startAngle = cumulativePercent * 2 * Math.PI;
                    const endAngle = (cumulativePercent + percent) * 2 * Math.PI;
                    cumulativePercent += percent;

                    const x1 = 90 + 70 * Math.cos(startAngle);
                    const y1 = 90 + 70 * Math.sin(startAngle);
                    const x2 = 90 + 70 * Math.cos(endAngle);
                    const y2 = 90 + 70 * Math.sin(endAngle);

                    const largeArcFlag = percent > 0.5 ? 1 : 0;

                    slices.push(
                      <path
                        key={idx}
                        d={`M 90 90 L ${x1} ${y1} A 70 70 0 ${largeArcFlag} 1 ${x2} ${y2} Z`}
                        fill={colors[idx % colors.length]}
                        stroke="white"
                        strokeWidth="2"
                      />
                    );
                  });

                  // Render "Other" slice if there are remaining holdings
                  if (otherPercent > 0.001) {
                    const startAngle = cumulativePercent * 2 * Math.PI;
                    const endAngle = 2 * Math.PI;

                    const x1 = 90 + 70 * Math.cos(startAngle);
                    const y1 = 90 + 70 * Math.sin(startAngle);
                    const x2 = 90 + 70 * Math.cos(endAngle);
                    const y2 = 90 + 70 * Math.sin(endAngle);

                    const largeArcFlag = otherPercent > 0.5 ? 1 : 0;

                    slices.push(
                      <path
                        key="other"
                        d={`M 90 90 L ${x1} ${y1} A 70 70 0 ${largeArcFlag} 1 ${x2} ${y2} Z`}
                        fill="#9CA3AF"
                        stroke="white"
                        strokeWidth="2"
                      />
                    );
                  }

                  return slices;
                })()}
              </svg>
            </div>

            {/* Legend */}
            <div className="grid grid-cols-2 gap-2">
              {metrics.topConcentrations.map((pos, idx) => {
                const colors = ['#3B82F6', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6'];
                return (
                  <div key={idx} className="flex items-center gap-2 text-xs">
                    <div
                      className="w-3 h-3 rounded-sm flex-shrink-0"
                      style={{ backgroundColor: colors[idx % colors.length] }}
                    />
                    <span className="text-gray-700 font-medium">{pos.ticker}</span>
                    <span className="text-gray-600 ml-auto">
                      {(pos.exposurePct * 100).toFixed(1)}%
                    </span>
                  </div>
                );
              })}
              {(() => {
                const topHoldingsPercent = metrics.topConcentrations.reduce((sum, pos) => sum + pos.exposurePct, 0);
                const otherPercent = 1 - topHoldingsPercent;
                if (otherPercent > 0.001) {
                  return (
                    <div className="flex items-center gap-2 text-xs">
                      <div
                        className="w-3 h-3 rounded-sm flex-shrink-0 bg-gray-400"
                      />
                      <span className="text-gray-700 font-medium">Other</span>
                      <span className="text-gray-600 ml-auto">
                        {(otherPercent * 100).toFixed(1)}%
                      </span>
                    </div>
                  );
                }
                return null;
              })()}
            </div>
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
