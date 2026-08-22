# Trading Scanner implementation plan

The repository is a single ASP.NET Core application in `dotnet-server` and a standalone-component Angular application in `angular-client`. The existing JWT/Identity flow remains in place while scanner modules are added alongside it.

1. **Foundation (this change):** establish domain types, application contracts, scanner configuration, PostgreSQL entities, SignalR hub, New York market-session boundaries, and the initial workstation shell.
2. **Market stream:** add one provider-owned Alpaca stock stream plus a synthetic provider, normalize trades/quotes/minute bars, and feed a concurrent per-symbol state manager.
3. **Indicators and scanner (complete):** incrementally maintain VWAP and rolling bars, introduce replaceable RVOL, momentum, scoring, heat, and filtered-view services, then publish coalesced snapshots.
4. **Live dashboard and catalysts (complete):** the Angular signal store hydrates over REST, consumes the SignalR JSON protocol, rejects stale snapshots, and reports reconnecting/degraded states. Alpaca news is ingested idempotently and classified by deterministic, ordered catalyst rules.
5. **Setups and alerts (complete):** isolated ORB, VWAP, pullback, premarket-high, and HOD detectors feed a composed setup engine. Meaningful alerts are edge-triggered, deduplicated, broadcast, and persisted with their signal context.
6. **Outcomes and refinement (complete):** schedule 1/5/15/30-minute signal measurements, expose historical analytics, profile hot paths, and tune models from measured results.

Alpaca credentials are intentionally deferred until the provider phase. They will be read from `ALPACA_API_KEY`, `ALPACA_API_SECRET`, and `ALPACA_DATA_FEED`; no order-routing surface will be introduced.
