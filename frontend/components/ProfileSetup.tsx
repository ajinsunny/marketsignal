'use client';

import { useState } from 'react';
import { api, type RiskProfile } from '@/lib/api';

interface ProfileSetupProps {
  isOpen: boolean;
  onClose: () => void;
  currentRiskProfile?: RiskProfile;
  currentCashBuffer?: number;
}

export default function ProfileSetup({
  isOpen,
  onClose,
  currentRiskProfile = 'Balanced',
  currentCashBuffer,
}: ProfileSetupProps) {
  const [riskProfile, setRiskProfile] = useState<RiskProfile>(currentRiskProfile);
  const [cashBuffer, setCashBuffer] = useState<string>(currentCashBuffer?.toString() || '');
  const [saving, setSaving] = useState(false);

  if (!isOpen) return null;

  const handleSave = async () => {
    setSaving(true);
    try {
      const cashValue = cashBuffer ? parseFloat(cashBuffer) : undefined;
      await api.updateProfile(riskProfile, cashValue);
      onClose();
    } catch (error) {
      console.error('Failed to save profile:', error);
      alert('Failed to save profile. Please try again.');
    } finally {
      setSaving(false);
    }
  };

  const riskProfiles: { value: RiskProfile; label: string; description: string; emoji: string }[] = [
    {
      value: 'Conservative',
      label: 'Conservative',
      description: 'Focus on capital preservation and stability',
      emoji: '🛡️',
    },
    {
      value: 'Balanced',
      label: 'Balanced',
      description: 'Moderate risk, balanced growth and safety',
      emoji: '⚖️',
    },
    {
      value: 'Aggressive',
      label: 'Aggressive',
      description: 'High risk tolerance, growth-focused',
      emoji: '🚀',
    },
  ];

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-2xl max-w-lg w-full p-6">
        <div className="flex justify-between items-start mb-6">
          <div>
            <h2 className="text-2xl font-bold text-slate-900">Set Your Preferences</h2>
            <p className="text-sm text-slate-600 mt-1">
              Help us personalize your recommendations
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 transition-colors"
          >
            <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Risk Profile Selection */}
        <div className="mb-6">
          <label className="block text-sm font-semibold text-slate-700 mb-3">
            Investment Style
          </label>
          <div className="space-y-3">
            {riskProfiles.map((profile) => (
              <button
                key={profile.value}
                onClick={() => setRiskProfile(profile.value)}
                className={`w-full text-left p-4 rounded-lg border-2 transition-all ${
                  riskProfile === profile.value
                    ? 'border-blue-500 bg-blue-50'
                    : 'border-slate-200 hover:border-slate-300 bg-white'
                }`}
              >
                <div className="flex items-center gap-3">
                  <span className="text-2xl">{profile.emoji}</span>
                  <div className="flex-1">
                    <div className="font-semibold text-slate-900">{profile.label}</div>
                    <div className="text-sm text-slate-600">{profile.description}</div>
                  </div>
                  {riskProfile === profile.value && (
                    <svg className="w-5 h-5 text-blue-500" fill="currentColor" viewBox="0 0 20 20">
                      <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                    </svg>
                  )}
                </div>
              </button>
            ))}
          </div>
        </div>

        {/* Cash Buffer Input */}
        <div className="mb-6">
          <label htmlFor="cashBuffer" className="block text-sm font-semibold text-slate-700 mb-2">
            Cash Buffer <span className="text-slate-400 font-normal">(Optional)</span>
          </label>
          <p className="text-xs text-slate-500 mb-2">
            Available cash for new opportunities. Helps us provide liquidity-aware recommendations.
          </p>
          <div className="relative">
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500">$</span>
            <input
              id="cashBuffer"
              type="number"
              value={cashBuffer}
              onChange={(e) => setCashBuffer(e.target.value)}
              placeholder="10000"
              className="w-full pl-8 pr-4 py-2 border border-slate-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none"
            />
          </div>
        </div>

        {/* Actions */}
        <div className="flex gap-3">
          <button
            onClick={onClose}
            className="flex-1 px-4 py-2 border border-slate-300 rounded-lg text-slate-700 hover:bg-slate-50 transition-colors font-medium"
          >
            Skip for Now
          </button>
          <button
            onClick={handleSave}
            disabled={saving}
            className="flex-1 px-4 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {saving ? 'Saving...' : 'Save & Continue'}
          </button>
        </div>
      </div>
    </div>
  );
}
