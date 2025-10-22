'use client';

import { useState, useEffect, useRef } from 'react';
import { api, Holding, Impact, AnalysisResult, RebalanceRecommendation, UserProfile, HoldingIntent, PortfolioMetrics, IntentMetrics } from '@/lib/api';
import { useRouter } from 'next/navigation';
import ProfileSetup from '@/components/ProfileSetup';
import EvidencePill, { AnalogTooltip } from '@/components/EvidencePill';
import PortfolioContext from '@/components/PortfolioContext';
import MarkdownText from '@/components/MarkdownText';
import ConfirmDialog from '@/components/ConfirmDialog';

export default function Dashboard() {
  const router = useRouter();
  const [holdings, setHoldings] = useState<Holding[]>([]);
  const [impacts, setImpacts] = useState<Impact[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [dragging, setDragging] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const pageSize = 10; // Items per page
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [analysis, setAnalysis] = useState<AnalysisResult | null>(null);
  const [loadingAnalysis, setLoadingAnalysis] = useState(false);
  const [showRationale, setShowRationale] = useState(false);
  const [expandedRecommendations, setExpandedRecommendations] = useState<Set<number>>(new Set());

  // Profile state
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [showProfileSetup, setShowProfileSetup] = useState(false);
  const [showProfileBanner, setShowProfileBanner] = useState(false);

  // PHASE 4B: Portfolio analytics state
  const [portfolioMetrics, setPortfolioMetrics] = useState<PortfolioMetrics | null>(null);
  const [intentMetrics, setIntentMetrics] = useState<Record<HoldingIntent, IntentMetrics> | null>(null);

  // Add holding form
  const [showAddForm, setShowAddForm] = useState(false);
  const [newHolding, setNewHolding] = useState({
    ticker: '',
    shares: '',
    costBasis: '',
    acquiredAt: '',
    intent: 'Hold' as HoldingIntent,
  });

  // Edit holding state
  const [editingHoldingId, setEditingHoldingId] = useState<string | null>(null);
  const [editValues, setEditValues] = useState({ shares: '', costBasis: '' });

  // Confirm dialog state
  const [confirmDialog, setConfirmDialog] = useState<{
    isOpen: boolean;
    title: string;
    message: string;
    onConfirm: () => void;
  }>({
    isOpen: false,
    title: '',
    message: '',
    onConfirm: () => {},
  });

  useEffect(() => {
    loadData();
  }, [currentPage]);

  const loadProfile = async () => {
    try {
      const profileData = await api.getProfile();
      setProfile(profileData);

      // Check if profile banner should be shown (stored in localStorage)
      const bannerDismissed = localStorage.getItem('profileBannerDismissed');
      if (!bannerDismissed) {
        setShowProfileBanner(true);
      }
    } catch (error) {
      console.error('Failed to load profile:', error);
      // Profile doesn't exist yet, show banner
      const bannerDismissed = localStorage.getItem('profileBannerDismissed');
      if (!bannerDismissed) {
        setShowProfileBanner(true);
      }
    }
  };

  // PHASE 4B: Load portfolio analytics
  const loadPortfolioAnalytics = async () => {
    try {
      const [metrics, intents] = await Promise.all([
        api.getPortfolioMetrics(),
        api.getIntentMetrics(),
      ]);
      setPortfolioMetrics(metrics);
      setIntentMetrics(intents);
    } catch (error) {
      console.error('Failed to load portfolio analytics:', error);
    }
  };

  const loadData = async () => {
    try {
      const [holdingsData, impactsData] = await Promise.all([
        api.getHoldings(),
        api.getImpacts(currentPage, pageSize),
      ]);
      setHoldings(holdingsData);
      setImpacts(impactsData.impacts);
      setTotalPages(impactsData.pagination.totalPages);
      setLoading(false);

      // Load profile
      await loadProfile();

      // PHASE 4B: Load portfolio analytics if we have holdings
      if (holdingsData.length > 0) {
        loadAnalysis();
        loadPortfolioAnalytics();
      }
    } catch (error) {
      console.error('Failed to load data:', error);
      router.push('/');
    }
  };

  const handleProfileSetupClose = async () => {
    setShowProfileSetup(false);
    await loadProfile(); // Reload profile after saving
  };

  const handleDismissBanner = () => {
    setShowProfileBanner(false);
    localStorage.setItem('profileBannerDismissed', 'true');
  };

  const loadAnalysis = async () => {
    setLoadingAnalysis(true);
    try {
      const analysisData = await api.getRebalanceSuggestions();
      setAnalysis(analysisData);
    } catch (error) {
      console.error('Failed to load analysis:', error);
    } finally {
      setLoadingAnalysis(false);
    }
  };

  const processImageFile = async (file: File) => {
    setUploading(true);
    try {
      // Extract tickers from image
      const result = await api.uploadPortfolioImage(file);

      if (!result.tickers || result.tickers.length === 0) {
        alert('No ticker symbols found in the image. Please try another image.');
        setUploading(false);
        return;
      }

      // Add all tickers as holdings with default values
      for (const ticker of result.tickers) {
        try {
          await api.addHolding(ticker, 1, 100);
        } catch (error) {
          console.error(`Failed to add holding for ${ticker}:`, error);
        }
      }

      // Reload holdings
      await loadData();

      // Trigger news fetch automatically
      await api.triggerNewsFetch();
      alert(`Added ${result.tickers.length} holdings from image. Fetching news...`);

      // Refresh data after 5 seconds to show impacts
      setTimeout(() => loadData(), 5000);
    } catch (error) {
      console.error('Failed to process image:', error);
      alert('Failed to process image. Please try again.');
    } finally {
      setUploading(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const handleImageUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    await processImageFile(file);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragging(false);
  };

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragging(false);

    const files = e.dataTransfer.files;
    if (files.length > 0) {
      const file = files[0];
      // Check if it's an image
      if (file.type.startsWith('image/')) {
        await processImageFile(file);
      } else {
        alert('Please drop an image file (JPEG, PNG, GIF, or WebP)');
      }
    }
  };

  const handleAddHolding = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.addHolding(
        newHolding.ticker.toUpperCase(),
        parseFloat(newHolding.shares),
        parseFloat(newHolding.costBasis),
        newHolding.acquiredAt || undefined,
        newHolding.intent
      );
      setNewHolding({ ticker: '', shares: '', costBasis: '', acquiredAt: '', intent: 'Hold' });
      setShowAddForm(false);
      await loadData();

      // Trigger news fetch automatically
      await api.triggerNewsFetch();

      // Refresh again after 3 seconds to show impacts
      setTimeout(() => loadData(), 3000);
    } catch (error) {
      console.error('Failed to add holding:', error);
      alert('Failed to add holding. Please try again.');
    }
  };

  const handleUpdateHoldingIntent = async (holdingId: number, intent: HoldingIntent) => {
    const holding = holdings.find((h) => h.id === holdingId);
    if (!holding) return;

    try {
      await api.updateHolding(
        holdingId,
        holding.shares,
        holding.costBasis,
        holding.acquiredAt,
        intent
      );
      await loadData();
    } catch (error) {
      console.error('Failed to update holding intent:', error);
      alert('Failed to update holding intent.');
    }
  };

  const handleDeleteHolding = (id: number) => {
    const holding = holdings.find(h => h.id === id);
    if (!holding) return;

    setConfirmDialog({
      isOpen: true,
      title: `Delete ${holding.ticker}?`,
      message: `Are you sure you want to delete this holding?\n\n${holding.shares} shares @ $${holding.costBasis}`,
      onConfirm: async () => {
        try {
          console.log(`Deleting holding with ID: ${id}`);
          await api.deleteHolding(id);
          await loadData();
        } catch (error) {
          console.error('Failed to delete holding:', error);
        } finally {
          setConfirmDialog({ ...confirmDialog, isOpen: false });
        }
      },
    });
  };

  const handleStartEdit = (holding: Holding) => {
    setEditingHoldingId(holding.id.toString());
    setEditValues({
      shares: holding.shares.toString(),
      costBasis: holding.costBasis.toString(),
    });
  };

  const handleCancelEdit = () => {
    setEditingHoldingId(null);
    setEditValues({ shares: '', costBasis: '' });
  };

  const handleSaveEdit = async (holdingId: number) => {
    const holding = holdings.find(h => h.id === holdingId);
    if (!holding) return;

    try {
      await api.updateHolding(
        holdingId,
        parseFloat(editValues.shares),
        parseFloat(editValues.costBasis),
        holding.acquiredAt,
        holding.intent
      );
      setEditingHoldingId(null);
      setEditValues({ shares: '', costBasis: '' });
      await loadData();
    } catch (error) {
      console.error('Failed to update holding:', error);
      alert('Failed to update holding.');
    }
  };

  const handleRefreshNews = async () => {
    setRefreshing(true);
    try {
      await api.triggerNewsFetch();
      alert('News fetch triggered! Impacts will update shortly.');
      setTimeout(() => loadData(), 5000);
    } catch (error) {
      console.error('Failed to trigger news fetch:', error);
      alert('Failed to trigger news fetch.');
    } finally {
      setRefreshing(false);
    }
  };

  const calculatePortfolioValue = () => {
    return holdings.reduce((sum, h) => sum + (h.shares * (h.costBasis || 0)), 0);
  };

  const getImpactColor = (score: number) => {
    if (score > 0.5) return 'text-green-600 bg-green-50';
    if (score > 0) return 'text-green-500 bg-green-50 bg-opacity-50';
    if (score < -0.5) return 'text-red-600 bg-red-50';
    if (score < 0) return 'text-red-500 bg-red-50 bg-opacity-50';
    return 'text-gray-500 bg-gray-50';
  };

  const getActionColor = (action: RebalanceRecommendation['action']) => {
    switch (action) {
      case 'StrongBuy':
        return 'bg-green-600 text-white';
      case 'Buy':
        return 'bg-green-500 text-white';
      case 'Hold':
        return 'bg-gray-500 text-white';
      case 'Sell':
        return 'bg-red-500 text-white';
      case 'StrongSell':
        return 'bg-red-600 text-white';
      default:
        return 'bg-gray-400 text-white';
    }
  };

  const getActionLabel = (action: RebalanceRecommendation['action']) => {
    switch (action) {
      case 'StrongBuy':
        return 'Strong Buy';
      case 'Buy':
        return 'Buy';
      case 'Hold':
        return 'Hold';
      case 'Sell':
        return 'Sell';
      case 'StrongSell':
        return 'Strong Sell';
      default:
        return action;
    }
  };

  const handlePreviousPage = () => {
    if (currentPage > 1) {
      setCurrentPage(currentPage - 1);
    }
  };

  const handleNextPage = () => {
    if (currentPage < totalPages) {
      setCurrentPage(currentPage + 1);
    }
  };

  const toggleRecommendationExpand = (index: number) => {
    const newExpanded = new Set(expandedRecommendations);
    if (newExpanded.has(index)) {
      newExpanded.delete(index);
    } else {
      newExpanded.add(index);
    }
    setExpandedRecommendations(newExpanded);
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="text-4xl mb-4">⏳</div>
          <p className="text-gray-600">Loading your portfolio...</p>
        </div>
      </div>
    );
  }

  const getRiskProfileEmoji = (riskProfile: string) => {
    switch (riskProfile) {
      case 'Conservative':
        return '🛡️';
      case 'Aggressive':
        return '🚀';
      default:
        return '⚖️';
    }
  };

  const getIntentEmoji = (intent: HoldingIntent) => {
    switch (intent) {
      case 'Trade':
        return '🎯';
      case 'Accumulate':
        return '📈';
      case 'Income':
        return '💰';
      default:
        return '🔒';
    }
  };

  const getSourceTierBadge = (tier?: string) => {
    if (!tier || tier === 'Unknown') return null;

    const tiers = {
      Premium: { label: 'Premium', color: 'bg-purple-100 text-purple-800 border-purple-300' },
      Standard: { label: 'Standard', color: 'bg-blue-100 text-blue-800 border-blue-300' },
      Official: { label: 'Official', color: 'bg-green-100 text-green-800 border-green-300' },
      Social: { label: 'Social', color: 'bg-gray-100 text-gray-800 border-gray-300' },
    };

    const config = tiers[tier as keyof typeof tiers];
    if (!config) return null;

    return (
      <span className={`px-2 py-0.5 rounded text-xs font-medium border ${config.color}`}>
        {config.label}
      </span>
    );
  };

  return (
    <>
      {/* Confirm Dialog */}
      <ConfirmDialog
        isOpen={confirmDialog.isOpen}
        title={confirmDialog.title}
        message={confirmDialog.message}
        onConfirm={confirmDialog.onConfirm}
        onCancel={() => setConfirmDialog({ ...confirmDialog, isOpen: false })}
        variant="danger"
        confirmLabel="Delete"
        cancelLabel="Cancel"
      />

      {/* Profile Setup Modal */}
      <ProfileSetup
        isOpen={showProfileSetup}
        onClose={handleProfileSetupClose}
        currentRiskProfile={profile?.riskProfile}
        currentCashBuffer={profile?.cashBuffer}
      />

      <div className="min-h-screen bg-gray-50">

      {/* Header - Sticky */}
      <div className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex justify-between items-center">
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold text-gray-900">Signal Copilot</h1>
              {profile && (
                <button
                  onClick={() => setShowProfileSetup(true)}
                  className="text-sm px-3 py-1 bg-slate-100 hover:bg-slate-200 rounded-full transition-colors flex items-center gap-1"
                >
                  <span>{getRiskProfileEmoji(profile.riskProfile)}</span>
                  <span className="text-slate-700 font-medium">{profile.riskProfile}</span>
                </button>
              )}
            </div>
            <button
              onClick={handleRefreshNews}
              disabled={refreshing}
              className="bg-blue-500 hover:bg-blue-600 text-white px-4 py-2 rounded-lg text-sm font-medium disabled:opacity-50"
            >
              {refreshing ? 'Refreshing...' : '🔄 Refresh News'}
            </button>
          </div>
        </div>

        {/* Profile Banner */}
        {showProfileBanner && (
          <div className="bg-blue-50 border-b border-blue-200">
            <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <span className="text-blue-600 text-xl">ℹ️</span>
                  <p className="text-sm text-blue-900">
                    <strong>Want better recommendations?</strong> Tell us about your investment style
                  </p>
                  <button
                    onClick={() => setShowProfileSetup(true)}
                    className="text-sm font-semibold text-blue-600 hover:text-blue-800 underline"
                  >
                    Set Preferences
                  </button>
                </div>
                <button
                  onClick={handleDismissBanner}
                  className="text-blue-400 hover:text-blue-600 transition-colors"
                >
                  <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Main Content - 3 Column Layout */}
      <div style={{ maxWidth: '1800px', margin: '0 auto', padding: '1rem' }}>
        {/* 3 Column Flexbox - PURE inline styles to force horizontal layout */}
        <div style={{
          display: 'flex',
          flexDirection: 'row',
          gap: '1rem',
          marginBottom: '1rem',
          width: '100%'
        }}>
          {/* Column 1: Your Portfolio - Scrollable */}
          <div className="bg-white rounded-lg shadow flex flex-col flex-shrink-0" style={{ width: '350px', maxHeight: 'calc(100vh - 6rem)' }}>
                {/* Portfolio Header & Controls */}
                <div className="p-4 border-b border-gray-200 flex-shrink-0">
                  <div className="flex justify-between items-center mb-3">
                    <h2 className="text-base font-semibold text-gray-900">Your Portfolio</h2>
                    <button
                      onClick={() => setShowAddForm(!showAddForm)}
                      className="flex items-center justify-center w-8 h-8 bg-blue-500 hover:bg-blue-600 text-white rounded-lg transition-colors shadow-sm"
                      title={showAddForm ? 'Close form' : 'Add holding'}
                    >
                      {showAddForm ? (
                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                        </svg>
                      ) : (
                        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                        </svg>
                      )}
                    </button>
                  </div>

                  {/* Image Upload with Drag & Drop - Compact */}
                  <div className="mb-3">
                    <input
                      ref={fileInputRef}
                      type="file"
                      accept="image/*"
                      onChange={handleImageUpload}
                      className="hidden"
                      id="portfolio-image-upload"
                    />
                    <label
                      htmlFor="portfolio-image-upload"
                      onDragOver={handleDragOver}
                      onDragLeave={handleDragLeave}
                      onDrop={handleDrop}
                      className={`block w-full px-3 py-2 border-2 border-dashed rounded-lg text-center cursor-pointer transition-colors ${
                        dragging
                          ? 'border-blue-500 bg-blue-50'
                          : 'border-gray-300 hover:border-blue-500'
                      } ${uploading ? 'opacity-50 cursor-not-allowed' : ''}`}
                    >
                      {uploading ? (
                        <div>
                          <div className="text-lg mb-0.5">⏳</div>
                          <span className="text-xs text-gray-600">Processing...</span>
                        </div>
                      ) : dragging ? (
                        <div>
                          <div className="text-lg mb-0.5">📥</div>
                          <span className="text-xs text-blue-600">Drop here</span>
                        </div>
                      ) : (
                        <div>
                          <div className="text-lg mb-0.5">📷</div>
                          <span className="text-xs text-gray-600">Upload Screenshot</span>
                        </div>
                      )}
                    </label>
                  </div>

                  {showAddForm && (
                    <form onSubmit={handleAddHolding} className="mb-4 p-4 bg-gray-50 rounded-lg">
                      <input
                        type="text"
                        placeholder="Ticker (e.g., AAPL)"
                        value={newHolding.ticker}
                        onChange={(e) => setNewHolding({ ...newHolding, ticker: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md mb-2 bg-white text-gray-900 placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                        required
                      />
                      <input
                        type="number"
                        placeholder="Shares"
                        step="0.01"
                        value={newHolding.shares}
                        onChange={(e) => setNewHolding({ ...newHolding, shares: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md mb-2 bg-white text-gray-900 placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                        required
                      />
                      <input
                        type="number"
                        placeholder="Cost Basis ($)"
                        step="0.01"
                        value={newHolding.costBasis}
                        onChange={(e) => setNewHolding({ ...newHolding, costBasis: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md mb-2 bg-white text-gray-900 placeholder:text-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                        required
                      />
                      <label className="block text-xs text-gray-600 mb-1">Acquisition Date (Optional)</label>
                      <input
                        type="date"
                        value={newHolding.acquiredAt}
                        onChange={(e) => setNewHolding({ ...newHolding, acquiredAt: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md mb-2 bg-white text-gray-900 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                      />
                      <label className="block text-xs text-gray-600 mb-1">Investment Intent</label>
                      <select
                        value={newHolding.intent}
                        onChange={(e) => setNewHolding({ ...newHolding, intent: e.target.value as HoldingIntent })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md mb-2 bg-white text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                      >
                        <option value="Trade">🎯 Trade</option>
                        <option value="Accumulate">📈 Accumulate</option>
                        <option value="Income">💰 Income</option>
                        <option value="Hold">🔒 Hold</option>
                      </select>
                      <button
                        type="submit"
                        className="w-full bg-blue-500 hover:bg-blue-600 text-white py-2 rounded-md"
                      >
                        Add Holding
                      </button>
                    </form>
                  )}

                  {/* Total Value - Compact */}
                  <div className="p-3 bg-blue-50 rounded-lg">
                    <p className="text-xs text-gray-600">Total Value</p>
                    <p className="text-lg font-bold text-gray-900">
                      ${calculatePortfolioValue().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    </p>
                  </div>
                </div>

                {/* Holdings List - Scrollable Area */}
                <div className="flex-1 overflow-y-auto p-4">
                  <div className="space-y-2">
                    {holdings.length === 0 ? (
                      <p className="text-gray-500 text-center py-8">
                        No holdings yet. Upload a portfolio screenshot or add stocks manually!
                      </p>
                    ) : (
                      holdings.map((holding) => (
                        <div key={holding.id} className="p-3 bg-gray-50 rounded-lg">
                          <div className="flex justify-between items-start mb-2">
                            <div className="flex-1">
                              <p className="font-semibold text-gray-900">{holding.ticker}</p>
                              {editingHoldingId === holding.id.toString() ? (
                                <div className="space-y-1 mt-1">
                                  <input
                                    type="number"
                                    step="0.01"
                                    value={editValues.shares}
                                    onChange={(e) => setEditValues({ ...editValues, shares: e.target.value })}
                                    placeholder="Shares"
                                    className="w-full text-xs px-2 py-1 border border-gray-300 rounded bg-white text-gray-900 placeholder:text-gray-400"
                                  />
                                  <input
                                    type="number"
                                    step="0.01"
                                    value={editValues.costBasis}
                                    onChange={(e) => setEditValues({ ...editValues, costBasis: e.target.value })}
                                    placeholder="Cost Basis"
                                    className="w-full text-xs px-2 py-1 border border-gray-300 rounded bg-white text-gray-900 placeholder:text-gray-400"
                                  />
                                  <div className="flex gap-1 mt-1">
                                    <button
                                      onClick={() => handleSaveEdit(holding.id)}
                                      className="flex-1 text-xs px-2 py-1 bg-blue-500 text-white rounded hover:bg-blue-600"
                                    >
                                      Save
                                    </button>
                                    <button
                                      onClick={handleCancelEdit}
                                      className="flex-1 text-xs px-2 py-1 bg-gray-300 text-gray-700 rounded hover:bg-gray-400"
                                    >
                                      Cancel
                                    </button>
                                  </div>
                                </div>
                              ) : (
                                <div className="flex items-center gap-2">
                                  <p className="text-sm text-gray-600">
                                    {holding.shares} shares @ ${holding.costBasis}
                                  </p>
                                  <button
                                    onClick={() => handleStartEdit(holding)}
                                    className="text-xs text-blue-500 hover:text-blue-600"
                                    title="Edit"
                                  >
                                    <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                                    </svg>
                                  </button>
                                </div>
                              )}
                            </div>
                            <button
                              onClick={() => handleDeleteHolding(holding.id)}
                              className="text-red-500 hover:text-red-600 ml-2"
                              title="Delete"
                            >
                              <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                              </svg>
                            </button>
                          </div>
                          {editingHoldingId !== holding.id.toString() && (
                            <div className="flex items-center gap-2">
                              <label className="text-xs text-gray-500">Intent:</label>
                              <select
                                value={holding.intent}
                                onChange={(e) => handleUpdateHoldingIntent(holding.id, e.target.value as HoldingIntent)}
                                className="text-xs px-2 py-1 border border-gray-300 rounded bg-white text-gray-900 hover:border-blue-400 focus:border-blue-500 focus:outline-none"
                              >
                                <option value="Trade">🎯 Trade</option>
                                <option value="Accumulate">📈 Accumulate</option>
                                <option value="Income">💰 Income</option>
                                <option value="Hold">🔒 Hold</option>
                              </select>
                            </div>
                          )}
                        </div>
                      ))
                    )}
                  </div>
                </div>
          </div>

          {/* Column 2: Portfolio Overview - Scrollable */}
          <div className="bg-white rounded-lg shadow flex flex-col flex-1" style={{ maxHeight: 'calc(100vh - 6rem)' }}>
            <div className="p-4 border-b border-gray-200 flex-shrink-0">
              <h2 className="text-base font-semibold text-gray-900">Portfolio Overview</h2>
            </div>
            <div className="flex-1 overflow-y-auto p-4">
              {portfolioMetrics && intentMetrics && holdings.length > 0 ? (
                <PortfolioContext
                  metrics={portfolioMetrics}
                  intentMetrics={intentMetrics}
                  riskProfile={profile?.riskProfile}
                  cashBuffer={profile?.cashBuffer}
                />
              ) : (
                <p className="text-gray-500 text-center py-8 text-sm">
                  Add holdings to see portfolio analysis
                </p>
              )}
            </div>
          </div>

          {/* Column 3: Impact Feed - Scrollable */}
          <div className="bg-white rounded-lg shadow flex flex-col flex-1" style={{ maxHeight: 'calc(100vh - 6rem)' }}>
            {/* Impact Feed Header */}
            <div className="p-4 border-b border-gray-200 flex-shrink-0">
              <h2 className="text-base font-semibold text-gray-900">Impact Feed</h2>
            </div>

            {impacts.length === 0 ? (
              <div className="p-8 flex-1 flex items-center justify-center">
                <div className="text-center">
                  <p className="text-gray-500 mb-2 text-sm">No impacts yet.</p>
                  <p className="text-xs text-gray-400">
                    Add holdings to see personalized impact scores. News is fetched automatically!
                  </p>
                </div>
              </div>
            ) : (
              <>
                {/* Impacts List - Scrollable */}
                <div className="flex-1 overflow-y-auto p-4">
                      <div className="space-y-4">
                        {impacts.map((impact) => (
                          <div
                            key={impact.id}
                            onClick={() => impact.article.sourceUrl && window.open(impact.article.sourceUrl, '_blank')}
                            className={`border border-gray-200 rounded-lg p-4 transition-all ${
                              impact.article.sourceUrl
                                ? 'cursor-pointer hover:shadow-lg hover:border-blue-300 hover:bg-blue-50/30'
                                : ''
                            } ${impact.impactScore > 0 ? 'bg-green-50/30' : impact.impactScore < 0 ? 'bg-red-50/30' : 'bg-gray-50/30'}`}
                          >
                            <div className="flex justify-between items-start mb-2">
                              <div className="flex items-center gap-2 flex-wrap">
                                <span className="font-bold text-gray-900">{impact.article.ticker}</span>
                                <span className={`px-2 py-1 rounded text-sm font-semibold ${getImpactColor(impact.impactScore)}`}>
                                  {impact.impactScore > 0 ? '+' : ''}{impact.impactScore.toFixed(4)}
                                </span>
                                {getSourceTierBadge(impact.article.sourceTier)}
                              </div>
                              <span className="text-xs text-gray-500 flex-shrink-0">
                                {new Date(impact.article.publishedAt).toLocaleDateString()}
                              </span>
                            </div>

                            <h3 className="font-medium text-gray-900 mb-2">
                              {impact.article.headline}
                            </h3>

                            {impact.article.summary && (
                              <p className="text-sm text-gray-600 mb-2 line-clamp-2">
                                {impact.article.summary}
                              </p>
                            )}

                            <div className="text-xs text-gray-500">
                              Exposure: {(impact.exposure * 100).toFixed(1)}% • {impact.article.publisher}
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>

                {/* Pagination - Fixed at bottom */}
                {totalPages > 1 && (
                  <div className="flex items-center justify-between border-t border-gray-200 p-4 flex-shrink-0">
                    <button
                      onClick={handlePreviousPage}
                      disabled={currentPage === 1}
                      className="px-4 py-2 text-sm font-medium text-white bg-blue-500 border border-blue-500 rounded-lg hover:bg-blue-600 disabled:opacity-50 disabled:cursor-not-allowed disabled:bg-gray-300 disabled:border-gray-300 transition-colors"
                    >
                      ← Previous
                    </button>
                    <span className="text-sm font-medium text-gray-700">
                      Page {currentPage} of {totalPages}
                    </span>
                    <button
                      onClick={handleNextPage}
                      disabled={currentPage === totalPages}
                      className="px-4 py-2 text-sm font-medium text-white bg-blue-500 border border-blue-500 rounded-lg hover:bg-blue-600 disabled:opacity-50 disabled:cursor-not-allowed disabled:bg-gray-300 disabled:border-gray-300 transition-colors"
                    >
                      Next →
                    </button>
                  </div>
                )}
              </>
            )}
          </div>
        </div>

        {/* Portfolio Analysis Section - Below 3 Columns */}
              {analysis && analysis.summary && (
                <div className="bg-white rounded-lg shadow">
                    {/* Analysis Header */}
                    <div className="p-6 border-b border-gray-200">
                      <div className="flex justify-between items-center">
                        <h2 className="text-lg font-semibold text-gray-900">Portfolio Analysis & Recommendations</h2>
                        <div className="text-sm text-gray-500">
                          {analysis.impactsAnalyzed} impacts analyzed
                        </div>
                      </div>
                    </div>

                    {/* Analysis Content - Two Column Layout */}
                    <div className="p-6">
                      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                        {/* LEFT COLUMN: Portfolio Summary Section */}
                        <div className="p-4 bg-blue-50 rounded-lg border border-blue-200">
                          <div className="flex items-start justify-between mb-3">
                            <div>
                              <h3 className="font-semibold text-blue-900 mb-1">Overall Portfolio Advice</h3>
                              <span className={`inline-block px-3 py-1 rounded-full text-xs font-semibold ${
                                analysis.summary.marketSentiment.includes('Positive')
                                  ? 'bg-green-100 text-green-800'
                                  : analysis.summary.marketSentiment.includes('Negative')
                                  ? 'bg-red-100 text-red-800'
                                  : 'bg-gray-100 text-gray-800'
                              }`}>
                                {analysis.summary.marketSentiment} Market
                              </span>
                            </div>
                          </div>

                          <MarkdownText text={analysis.summary.overallAdvice} className="text-gray-800 mb-4 leading-relaxed" />

                          {/* Key Actions */}
                          <div className="mb-4">
                            <h4 className="font-semibold text-sm text-blue-900 mb-2">Recommended Actions:</h4>
                            <ul className="space-y-2">
                              {analysis.summary.keyActions.map((action, i) => (
                                <li key={i} className="flex items-start gap-2 text-sm text-gray-700">
                                  <span className="text-blue-600 mt-1">•</span>
                                  <span>{action}</span>
                                </li>
                              ))}
                            </ul>
                          </div>

                          {/* Risk Assessment */}
                          <div className="p-3 bg-amber-50 border border-amber-200 rounded">
                            <h4 className="font-semibold text-sm text-amber-900 mb-1">Risk Assessment:</h4>
                            <p className="text-sm text-amber-800">{analysis.summary.riskAssessment}</p>
                          </div>

                          {/* Expandable Rationale */}
                          <button
                            onClick={() => setShowRationale(!showRationale)}
                            className="mt-4 text-sm text-blue-600 hover:text-blue-800 font-medium flex items-center gap-1"
                          >
                            {showRationale ? '▼' : '▶'} View Detailed Rationale
                          </button>

                          {showRationale && (
                            <div className="mt-3 p-4 bg-white rounded border border-blue-200">
                              <MarkdownText text={analysis.summary.rationale} className="text-sm text-gray-700" />
                            </div>
                          )}
                        </div>

                        {/* RIGHT COLUMN: Individual Recommendations */}
                        {analysis.recommendations.length > 0 && (
                          <div>
                            <h3 className="font-semibold text-gray-900 mb-3">Individual Stock Recommendations</h3>
                        <div className="space-y-3">
                          {analysis.recommendations.map((rec, index) => (
                            <div key={index} className="border border-gray-200 rounded-lg p-4 hover:shadow-md transition-shadow">
                              <div className="flex justify-between items-start mb-2">
                                <div className="flex items-center gap-3">
                                  <span className="font-bold text-lg text-gray-900">{rec.ticker}</span>
                                  <span className={`px-3 py-1 rounded-full text-sm font-semibold ${getActionColor(rec.action)}`}>
                                    {getActionLabel(rec.action)}
                                  </span>
                                </div>
                                <div className="text-right">
                                  <div className="text-xs text-gray-500">Confidence</div>
                                  <div className="text-sm font-semibold text-gray-900">
                                    {(rec.confidenceScore * 100).toFixed(0)}%
                                  </div>
                                </div>
                              </div>

                              <p className="text-sm font-medium text-gray-900 mb-2">{rec.suggestion}</p>

                              {/* PHASE 4A: Evidence Pills */}
                              <EvidencePill
                                analogs={rec.analogs}
                                confidence={rec.confidenceScore}
                                sourceTier={rec.sourceTier}
                              />

                              <button
                                onClick={() => toggleRecommendationExpand(index)}
                                className="text-xs text-blue-600 hover:text-blue-800 mb-2 mt-3"
                              >
                                {expandedRecommendations.has(index) ? '▼ Hide details' : '▶ Show reasoning'}
                              </button>

                              {expandedRecommendations.has(index) && (
                                <div className="mt-2 p-3 bg-gray-50 rounded border border-gray-200">
                                  <p className="text-sm text-gray-700 mb-2">{rec.reasoning}</p>

                                  {/* Show detailed analog pattern if available */}
                                  {rec.analogs && rec.analogs.count > 0 && (
                                    <AnalogTooltip analogs={rec.analogs} />
                                  )}
                                  <div className="flex flex-wrap items-center gap-2 text-xs text-gray-500 mt-2">
                                    <span className="px-2 py-1 bg-gray-100 rounded">
                                      {rec.sourceTier} Sources
                                    </span>
                                    <span className="px-2 py-1 bg-gray-100 rounded">
                                      {rec.newsCount} articles
                                    </span>
                                    {rec.keySignals.length > 0 && (
                                      <>
                                        <span>•</span>
                                        {rec.keySignals.map((signal, i) => (
                                          <span key={i} className="px-2 py-1 bg-blue-50 text-blue-700 rounded">
                                            {signal}
                                          </span>
                                        ))}
                                      </>
                                    )}
                                  </div>
                                </div>
                              )}
                            </div>
                          ))}
                        </div>
                          </div>
                        )}
                      </div>
                    </div>
                </div>
              )}
        </div>
      </div>
    </>
  );
}
