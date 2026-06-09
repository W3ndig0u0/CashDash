# CashDash 💸

CashDash is an automated, real-time routing engine and analytics dashboard that solves the lack of transparency in international cross-border money transfers. It runs concurrent live market checks to show users whether it is cheaper and faster to send capital via **Web3 Blockchains**, **Modern Fintech Platforms (Wise)**, or **Traditional Banking Rails (SWIFT)**.

Unlike typical demo apps, CashDash uses **zero hardcoded dummy data for pricing**, pulling live network congestion, withdrawal schedules, currency spreads, and corporate fee structures directly from production APIs.

---

## 🚀 Key Features

*   **100% Live Market Data:** No simulations. Gas prices, exchange rates, and exchange fees are computed dynamically on every execution.
*   **Multiverse Payment Routing:** Compares 6 distinct blockkedjor (Solana, Base, Arbitrum, Polygon, Tron, Ethereum) against fintech and institutional banking side-by-side.
*   **Custom Stockmarket Data Visualization:** A high-performance, lightweight SVG line graph and cost index built natively in Angular (no heavy chart libraries) that maps out transaction efficiency curves in real time.
*   **Traditional Finance Realism:** Accounts for hidden correspondent bank fees ($15 SWIFT intermediary cuts) and dynamic weekend processing delays.

---

## 🛠️ The Tech Stack

### Backend (.NET Core Web API)
*   **C# / .NET:** High-performance, asynchronous pipeline using concurrent `Task.WhenAll` mapping.
*   **In-Memory Caching:** Strict time-to-live (TTL) cache limits per node (e.g., 15s for volatile gas, 1h for exchange rates) to respect external API rate limits while maintaining accurate data.

### Frontend (Angular)
*   **Standalone Architecture:** Lightweight components utilizing the modern `@for` control flow.
*   **Custom Angular Pipes:** Dynamic temporal formatting (`timeFormatter`) mapping raw UNIX seconds into human-readable minutes, hours, or banking days.
*   **Tailwind CSS:** Ultra-minimalist, sleek SaaS aesthetic inspired by platforms like Stripe and Apple.

---

## 📡 Live Production Data Integrations

To build a truly objective engine, the backend orchestrates concurrent connections to the following protocols on every calculation request:

1.  **Etherscan API (V2):** Fetches active Oracle `ProposeGasPrice` in Gwei dynamically mapped across multiple layers (Ethereum Mainnet, Base, Arbitrum, Polygon).
2.  **KuCoin Public API:** Dynamically pulls live exchange withdrawal fee rates for stablecoins (`USDC`/`USDT`) across varied blockchain specifications to account for CEX on/off-ramp frictional costs.
3.  **Kraken Public API:** Monitors spot orderbook `Taker Fees` directly from the exchange layers to track true fiat-to-crypto entry costs.
4.  **Wise (TransferWise) API:** Pulls live commercial quotes using active source/target currency corridors.
5.  **ExchangeRate-API:** Monitors live mid-market global foreign exchange (FX) rates for 10+ major fiat currencies against the Swedish Krona (SEK).

---

## 🧬 Core Architecture Preview

The backend dynamically measures the cost efficiency of crypto routes by factoring in exchange trading friction, network withdrawal overhead, and exact smart contract interaction limitations:

$$Total Web3 Cost = Trading Fee + Withdrawal Fee + Gas Limit \times Gas Price$$

The frontend interprets this output array, converts transaction velocities and costs into absolute coordinate space ($X$ and $Y$), and instantly projects a responsive vector stockmarket trendline.
