'use client';

/**
 * PHASE 4A: EvidencePill Component
 *
 * Displays historical evidence for recommendations, showing users
 * concrete patterns from similar past events.
 *
 * Purpose: Break the "same recommendation loop" by providing
 * transparent, data-backed reasoning for each recommendation.
 */

export interface AnalogData {
  count: number;
  pattern: string;
  medianMove5D?: number;
  medianMove30D?: number;
}

interface EvidencePillProps {
  analogs?: AnalogData | null;
  confidence?: number;
  sourceTier?: string;
}

export default function EvidencePill({ analogs, confidence, sourceTier }: EvidencePillProps) {
  // Only show if we have meaningful evidence
  if (!analogs && !confidence && !sourceTier) {
    return null;
  }

  return (
    <div className="flex flex-wrap gap-2 mt-2">
      {/* Historical Analogs Pill */}
      {analogs && analogs.count > 0 && (
        <div
          className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-purple-50 border border-purple-200 rounded-full text-xs font-medium text-purple-700 hover:bg-purple-100 transition-colors"
          title={analogs.pattern}
        >
          <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
          <span className="whitespace-nowrap">
            {analogs.count} similar event{analogs.count !== 1 ? 's' : ''}
          </span>
        </div>
      )}

      {/* Confidence Score Pill */}
      {confidence !== undefined && (
        <div
          className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
            confidence >= 0.7
              ? 'bg-green-50 border-green-200 text-green-700 hover:bg-green-100'
              : confidence >= 0.4
              ? 'bg-yellow-50 border-yellow-200 text-yellow-700 hover:bg-yellow-100'
              : 'bg-slate-50 border-slate-200 text-slate-600 hover:bg-slate-100'
          }`}
          title={`Signal confidence: ${(confidence * 100).toFixed(0)}%`}
        >
          <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <span className="whitespace-nowrap">
            {(confidence * 100).toFixed(0)}% confidence
          </span>
        </div>
      )}

      {/* Source Tier Pill */}
      {sourceTier && (
        <div
          className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
            sourceTier === 'Premium' || sourceTier === 'High Quality'
              ? 'bg-blue-50 border-blue-200 text-blue-700 hover:bg-blue-100'
              : 'bg-slate-50 border-slate-200 text-slate-600 hover:bg-slate-100'
          }`}
          title={`Source quality: ${sourceTier}`}
        >
          <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
          </svg>
          <span className="whitespace-nowrap">
            {sourceTier}
          </span>
        </div>
      )}
    </div>
  );
}

/**
 * Tooltip variant that shows the full historical pattern
 * Use this when you want to display the detailed analog pattern
 */
interface AnalogTooltipProps {
  analogs: AnalogData;
}

export function AnalogTooltip({ analogs }: AnalogTooltipProps) {
  return (
    <div className="mt-3 p-3 bg-purple-50 border border-purple-200 rounded-lg">
      <div className="flex items-start gap-2">
        <svg className="w-4 h-4 text-purple-600 mt-0.5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <div className="flex-1">
          <div className="text-xs font-semibold text-purple-900 mb-1">
            Historical Pattern
          </div>
          <div className="text-xs text-purple-700 leading-relaxed">
            {analogs.pattern}
          </div>
        </div>
      </div>
    </div>
  );
}
