'use client';

import { useState, useEffect, useRef } from 'react';
import { api, Holding, Impact, AnalysisResult, RebalanceRecommendation } from '@/lib/api';
import { useRouter } from 'next/navigation';

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

  // Add holding form
  const [showAddForm, setShowAddForm] = useState(false);
  const [newHolding, setNewHolding] = useState({ ticker: '', shares: '', costBasis: '' });

  useEffect(() => {
    loadData();
  }, [currentPage]);

  const loadData = async () => {
    try {
      const [holdingsData, impactsData] = await Promise.all([
        api.getHoldings(),
        api.getImpacts(currentPage, pageSize)
      ]);
      setHoldings(holdingsData);
      setImpacts(impactsData.impacts);
      setTotalPages(impactsData.pagination.totalPages);
      setLoading(false);

      // Load analysis if we have holdings
      if (holdingsData.length > 0) {
        loadAnalysis();
      }
    } catch (error) {
      console.error('Failed to load data:', error);
      router.push('/');
    }
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
        parseFloat(newHolding.costBasis)
      );
      setNewHolding({ ticker: '', shares: '', costBasis: '' });
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

  const handleDeleteHolding = async (id: number) => {
    if (!confirm('Are you sure you want to remove this holding?')) return;
    try {
      await api.deleteHolding(id);
      await loadData();
    } catch (error) {
      console.error('Failed to delete holding:', error);
      alert('Failed to delete holding.');
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
    if (score > 0) return 'text-green-500 bg-green-50/50';
    if (score < -0.5) return 'text-red-600 bg-red-50';
    if (score < 0) return 'text-red-500 bg-red-50/50';
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

  return (
    <div className="h-screen bg-gray-50 flex flex-col overflow-hidden">
      {/* Header - Fixed */}
      <div className="bg-white border-b border-gray-200 flex-shrink-0">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex justify-between items-center">
            <h1 className="text-2xl font-bold text-gray-900">Signal Copilot</h1>
            <button
              onClick={handleRefreshNews}
              disabled={refreshing}
              className="bg-blue-500 hover:bg-blue-600 text-white px-4 py-2 rounded-lg text-sm font-medium disabled:opacity-50"
            >
              {refreshing ? 'Refreshing...' : '🔄 Refresh News'}
            </button>
          </div>
        </div>
      </div>

      {/* Main Content - Scrollable */}
      <div className="flex-1 overflow-hidden">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 h-full">
          <div className="grid grid-cols-1 lg:grid-cols-4 gap-8 h-full">
            {/* Portfolio Section */}
            <div className="lg:col-span-1 h-full overflow-hidden">
              <div className="bg-white rounded-lg shadow h-full flex flex-col">
                {/* Portfolio Header & Controls */}
                <div className="p-6 border-b border-gray-200 flex-shrink-0">
                  <div className="flex justify-between items-center mb-4">
                    <h2 className="text-lg font-semibold text-gray-900">Your Portfolio</h2>
                    <button
                      onClick={() => setShowAddForm(!showAddForm)}
                      className="text-blue-500 hover:text-blue-600 text-2xl"
                    >
                      {showAddForm ? '✕' : '+'}
                    </button>
                  </div>

                  {/* Image Upload with Drag & Drop */}
                  <div className="mb-4">
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
                      className={`block w-full px-4 py-3 border-2 border-dashed rounded-lg text-center cursor-pointer transition-colors ${
                        dragging
                          ? 'border-blue-500 bg-blue-50'
                          : 'border-gray-300 hover:border-blue-500'
                      } ${uploading ? 'opacity-50 cursor-not-allowed' : ''}`}
                    >
                      {uploading ? (
                        <div>
                          <div className="text-2xl mb-1">⏳</div>
                          <span className="text-sm text-gray-600">Processing image...</span>
                        </div>
                      ) : dragging ? (
                        <div>
                          <div className="text-2xl mb-1">📥</div>
                          <span className="text-sm text-blue-600">Drop image here</span>
                        </div>
                      ) : (
                        <div>
                          <div className="text-2xl mb-1">📷</div>
                          <span className="text-sm text-gray-600">Upload or Drop Portfolio Screenshot</span>
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
                        className="w-full px-3 py-2 border border-gray-300 rounded-md mb-2"
                        required
                      />
                      <input
                        type="number"
                        placeholder="Shares"
                        step="0.01"
                        value={newHolding.shares}
                        onChange={(e) => setNewHolding({ ...newHolding, shares: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md mb-2"
                        required
                      />
                      <input
                        type="number"
                        placeholder="Cost Basis ($)"
                        step="0.01"
                        value={newHolding.costBasis}
                        onChange={(e) => setNewHolding({ ...newHolding, costBasis: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md mb-2"
                        required
                      />
                      <button
                        type="submit"
                        className="w-full bg-blue-500 hover:bg-blue-600 text-white py-2 rounded-md"
                      >
                        Add Holding
                      </button>
                    </form>
                  )}

                  {/* Total Value */}
                  <div className="p-4 bg-blue-50 rounded-lg">
                    <p className="text-sm text-gray-600">Total Value</p>
                    <p className="text-2xl font-bold text-gray-900">
                      ${calculatePortfolioValue().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    </p>
                  </div>
                </div>

                {/* Holdings List - Scrollable Area */}
                <div className="flex-1 overflow-y-auto p-6">
                  <div className="space-y-3">
                    {holdings.length === 0 ? (
                      <p className="text-gray-500 text-center py-8">
                        No holdings yet. Upload a portfolio screenshot or add stocks manually!
                      </p>
                    ) : (
                      holdings.map((holding) => (
                        <div key={holding.id} className="flex justify-between items-center p-3 bg-gray-50 rounded-lg">
                          <div>
                            <p className="font-semibold text-gray-900">{holding.ticker}</p>
                            <p className="text-sm text-gray-600">
                              {holding.shares} shares @ ${holding.costBasis}
                            </p>
                          </div>
                          <button
                            onClick={() => handleDeleteHolding(holding.id)}
                            className="text-red-500 hover:text-red-600"
                          >
                            🗑️
                          </button>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              </div>
            </div>

            {/* Right Column - Analysis and Impacts */}
            <div className="lg:col-span-3 h-full overflow-hidden flex flex-col gap-8">
              {/* Portfolio Analysis Section */}
              {analysis && analysis.recommendations.length > 0 && (
                <div className="bg-white rounded-lg shadow flex flex-col max-h-[40%]">
                  {/* Analysis Header */}
                  <div className="p-6 border-b border-gray-200 flex-shrink-0">
                    <div className="flex justify-between items-center">
                      <h2 className="text-lg font-semibold text-gray-900">Portfolio Analysis</h2>
                      <div className="text-sm text-gray-500">
                        {analysis.impactsAnalyzed} impacts analyzed
                      </div>
                    </div>
                  </div>

                  {/* Analysis List - Scrollable */}
                  <div className="flex-1 overflow-y-auto p-6">
                    <div className="space-y-4">
                      {analysis.recommendations.map((rec, index) => (
                        <div key={index} className="border border-gray-200 rounded-lg p-4 hover:shadow-md transition-shadow">
                          <div className="flex justify-between items-start mb-3">
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
                          <p className="text-sm text-gray-600 mb-3">{rec.reasoning}</p>

                          <div className="flex flex-wrap items-center gap-2 text-xs text-gray-500">
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
                      ))}
                    </div>
                  </div>
                </div>
              )}

              {/* Impacts Section */}
              <div className="h-full overflow-hidden flex-1">
                <div className="bg-white rounded-lg shadow h-full flex flex-col">
                  {/* Impact Feed Header */}
                  <div className="p-6 border-b border-gray-200 flex-shrink-0">
                    <h2 className="text-lg font-semibold text-gray-900">Impact Feed</h2>
                  </div>

                {impacts.length === 0 ? (
                  <div className="flex-1 flex items-center justify-center p-6">
                    <div className="text-center">
                      <p className="text-gray-500 mb-4">No impacts yet.</p>
                      <p className="text-sm text-gray-400">
                        Add holdings to see personalized impact scores. News is fetched automatically!
                      </p>
                    </div>
                  </div>
                ) : (
                  <>
                    {/* Impacts List - Scrollable Area */}
                    <div className="flex-1 overflow-y-auto p-6">
                      <div className="space-y-4">
                        {impacts.map((impact) => (
                          <div key={impact.id} className="border border-gray-200 rounded-lg p-4 hover:shadow-md transition-shadow">
                            <div className="flex justify-between items-start mb-2">
                              <div className="flex items-center gap-2">
                                <span className="font-bold text-gray-900">{impact.article.ticker}</span>
                                <span className={`px-2 py-1 rounded text-sm font-semibold ${getImpactColor(impact.impactScore)}`}>
                                  {impact.impactScore > 0 ? '+' : ''}{impact.impactScore.toFixed(4)}
                                </span>
                              </div>
                              <span className="text-xs text-gray-500">
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

                            <div className="flex justify-between items-center text-xs text-gray-500">
                              <span>
                                Exposure: {(impact.exposure * 100).toFixed(1)}% • {impact.article.publisher}
                              </span>
                              {impact.article.sourceUrl && (
                                <a
                                  href={impact.article.sourceUrl}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                  className="text-blue-500 hover:text-blue-600"
                                >
                                  Read more →
                                </a>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>

                    {/* Pagination - Fixed at Bottom */}
                    {totalPages > 1 && (
                      <div className="flex items-center justify-between border-t border-gray-200 p-6 flex-shrink-0">
                        <button
                          onClick={handlePreviousPage}
                          disabled={currentPage === 1}
                          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                          Previous
                        </button>
                        <span className="text-sm text-gray-700">
                          Page {currentPage} of {totalPages}
                        </span>
                        <button
                          onClick={handleNextPage}
                          disabled={currentPage === totalPages}
                          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                          Next
                        </button>
                      </div>
                    )}
                  </>
                )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
