# 🚦 Signal Copilot – Lite

> **"Know what actually matters to your portfolio — without the noise."**

Signal Copilot – Lite is a lightweight ASP.NET-based web app that converts financial news into **personalized impact alerts** for a user’s holdings.  
It blends simple rule-based sentiment scoring with an AI summarizer to explain _why_ each event matters.

---

## 🌟 Core Idea

Investors drown in information.  
Signal Copilot filters and ranks headlines by **relevance × impact × confidence**, surfacing only what truly affects your portfolio.

---

## 🧩 Features (MVP Scope)

- 📈 Add or import holdings (ticker + position size).
- 📰 Pull recent news headlines for tracked tickers.
- 🤖 Analyze sentiment, magnitude, and confidence per article.
- 🧮 Compute an **impact score** per ticker for each user.
- ⚡ Send **daily digests** and **high-impact alerts** via UI or email.
- 🔍 Show clear explanations and source links for every alert.

> **Out of Scope for v1:** trading execution, social-media scraping, paid research feeds, or alpha predictions.

---

## 🏗️ System Overview

User (Web UI)
│
├─► Portfolio API (Add/Edit tickers)
│
├─► News Ingestor (fetch public headlines)
│
├─► Analyzer (relevance + sentiment + magnitude + confidence)
│
├─► Scorer (combines exposure → impact)
│
├─► Notifier (digest + alerts)
│
└─► UI Feed (cards + rationale + feedback)

---

## 📊 Core Concepts

| Concept          | Description                                      |
| ---------------- | ------------------------------------------------ |
| **Sentiment**    | Direction of news (positive / negative).         |
| **Magnitude**    | Strength of event (`1` = minor, `3` = major).    |
| **Confidence**   | Source credibility × cross-source agreement.     |
| **Exposure**     | User’s position weight for the ticker.           |
| **Impact Score** | `direction × magnitude × confidence × exposure`. |

Only events with |impact| ≥ threshold trigger alerts.

---

## 🧱 Suggested Tech Stack

| Layer            | Choice                              |
| ---------------- | ----------------------------------- |
| Backend          | ASP.NET Core 9 (Web API)            |
| Database         | PostgreSQL / SQL Server via EF Core |
| Scheduler        | Hangfire / Quartz.NET               |
| Frontend         | Razor / React SPA                   |
| AI Summarization | Azure OpenAI GPT-4o Mini (optional) |
| Auth             | ASP.NET Identity + JWT              |
| Notifications    | SendGrid / SMTP email               |

---

## 🗃️ Minimal Data Model

```csharp
User(id, email, timezone)
Holding(id, user_id, ticker, shares, cost_basis)
Article(id, ticker, title, url, publisher, published_at)
Signal(id, article_id, ticker, sentiment, magnitude, confidence)
Impact(id, user_id, ticker, article_id, impact_score)
Alert(id, user_id, created_at, type, items_json)

```

## Quick-Start Roadmap

1.  **Initialize ASP.NET API**

    - dotnet new webapi -n SignalCopilotLite
    - Set up EF Core + DB context.

2.  **Create Portfolio Endpoints**

    - CRUD for holdings.

3.  **Integrate News Fetcher**

    - Start with a free news API (e.g., NewsAPI.org).

4.  **Implement Analyzer Module**

    - Keyword-based sentiment / magnitude rules.
    - Confidence via source tiering.

5.  **Compute Impact**

    - Combine metrics + user exposure.

6.  **Deliver Alerts**

    - Background job (daily digest).
    - Razor page / email template.

7.  **Polish UI**

    - Simple dashboard + alert feed.

8.  **Feedback Loop**

    - Allow “useful / noise” tagging for future tuning.

## Safety & Ethics

- Clearly label: _“Awareness tool. Not financial advice.”_
- Filter unverified/rumor headlines.
- Respect robots.txt / API ToS.
- Encrypt user data and holdings.
- Store minimal personal info.

## 🧭 Future Enhancements

- Sector-specific packs (AI chips, energy, biotech).
- Alternative-data signals (app ranks, web traffic).
- Historical analogs & confidence calibration.
- Broker read-only sync (Plaid / Alpaca).
- Community signal verification.

## 🧑‍💻 Contributing

Pull requests welcome!Follow standard C# style guidelines and write unit tests for core scoring logic.

## 📜 License

MIT License © 2025 AjinThis project is for educational and non-advisory use only.
