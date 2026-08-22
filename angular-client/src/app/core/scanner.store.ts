import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

export type ConnectionState = 'connecting' | 'live' | 'degraded' | 'offline';
export interface ScoreComponents { catalyst: number; relativeVolume: number; gapMomentum: number; float: number; premarketVolume: number; volumeAcceleration: number; vwap: number; setup: number; resistanceRoom: number; liquidity: number; }
export interface TickerState { symbol: string; price: number; changePercent: number; gapPercent: number; relativeVolume: number; floatShares: number | null; volume: number; momentumState: number; currentSetup: number; vwap: number; highOfDay: number; spreadPercent: number; catalystHeadline?: string; catalystType: number; catalystQuality: number; aPlusScore: { totalScore: number; grade: number; components: ScoreComponents }; lastUpdated: string; }
export interface ScannerSnapshot { sequence: number; generatedAt: string; session: number; marketHeat: number; tickers: TickerState[]; }
export interface NewsItem { id: number; headline: string; source?: string; symbols: string[]; catalystType: number; catalystQuality: number; publishedAt: string; }
interface Hydration { snapshot: ScannerSnapshot; news: NewsItem[]; }
interface Negotiation { connectionToken: string; availableTransports: { transport: string }[]; }

const recordSeparator = '\u001e';

@Injectable({ providedIn: 'root' })
export class ScannerStore {
  private readonly http = inject(HttpClient);
  private socket?: WebSocket;
  private reconnectTimer?: ReturnType<typeof setTimeout>;
  private staleTimer?: ReturnType<typeof setInterval>;
  private attempt = 0;
  private stopped = true;

  readonly connection = signal<ConnectionState>('offline');
  readonly snapshot = signal<ScannerSnapshot | null>(null);
  readonly news = signal<NewsItem[]>([]);
  readonly lastDataAt = signal<Date | null>(null);
  readonly isDemo = signal(false);
  readonly tickers = computed(() => this.snapshot()?.tickers ?? []);

  async start(): Promise<void> {
    if (!this.stopped) return;
    this.stopped = false;
    this.connection.set('connecting');
    try {
      const hydration = await firstValueFrom(this.http.get<Hydration>(`${environment.apiBaseUrl}/scanner/dashboard`));
      this.acceptSnapshot(hydration.snapshot);
      this.news.set(hydration.news);
    } catch {
      this.acceptSnapshot(demoSnapshot());
      this.news.set(demoNews());
      this.isDemo.set(true);
      this.connection.set('degraded');
    }
    this.connect();
    this.staleTimer = setInterval(() => {
      const age = Date.now() - (this.lastDataAt()?.getTime() ?? 0);
      if (this.connection() === 'live' && age > 10_000) this.connection.set('degraded');
    }, 2_000);
  }

  stop(): void {
    this.stopped = true;
    clearTimeout(this.reconnectTimer);
    clearInterval(this.staleTimer);
    this.socket?.close();
    this.connection.set('offline');
  }

  private async connect(): Promise<void> {
    if (this.stopped) return;
    this.connection.set(this.snapshot() ? 'degraded' : 'connecting');
    try {
      const hubUrl = environment.apiBaseUrl.replace(/\/api\/?$/, '/hubs/market');
      const negotiation = await firstValueFrom(this.http.post<Negotiation>(`${hubUrl}/negotiate?negotiateVersion=1`, {}));
      if (!negotiation.availableTransports.some(x => x.transport === 'WebSockets')) throw new Error('WebSockets unavailable');
      const wsUrl = `${hubUrl.replace(/^http/, 'ws')}?id=${encodeURIComponent(negotiation.connectionToken)}`;
      const socket = this.socket = new WebSocket(wsUrl);
      socket.onopen = () => socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + recordSeparator);
      socket.onmessage = event => this.receive(String(event.data));
      socket.onerror = () => socket.close();
      socket.onclose = () => this.scheduleReconnect();
    } catch { this.scheduleReconnect(); }
  }

  private receive(payload: string): void {
    for (const frame of payload.split(recordSeparator).filter(Boolean)) {
      const message = JSON.parse(frame) as { type?: number; target?: string; arguments?: unknown[]; error?: string };
      if (message.error) { this.socket?.close(); continue; }
      if (message.type === undefined) { this.attempt = 0; this.connection.set('live'); continue; }
      if (message.type === 1 && message.target === 'ScannerSnapshotReceived')
        this.acceptSnapshot(message.arguments?.[0] as ScannerSnapshot);
    }
  }

  private acceptSnapshot(snapshot: ScannerSnapshot): void {
    if ((this.snapshot()?.sequence ?? -1) > snapshot.sequence && snapshot.sequence !== 0) return;
    this.snapshot.set(snapshot);
    this.lastDataAt.set(new Date(snapshot.generatedAt));
    if (snapshot.sequence !== -1) this.isDemo.set(false);
  }

  private scheduleReconnect(): void {
    if (this.stopped) return;
    this.connection.set(this.snapshot() ? 'degraded' : 'offline');
    const delay = Math.min(30_000, 1_000 * 2 ** Math.min(this.attempt++, 5));
    this.reconnectTimer = setTimeout(() => this.connect(), delay);
  }
}

function demoSnapshot(): ScannerSnapshot {
  const now = new Date().toISOString();
  const ticker = (symbol: string, price: number, change: number, score: number, offset: number): TickerState => ({
    symbol, price, changePercent: change, gapPercent: change - 1.4, relativeVolume: 3.2 + offset,
    floatShares: 7_800_000 + offset * 1_100_000, volume: 1_840_000 + offset * 420_000,
    momentumState: offset > 1 ? 3 : 2, currentSetup: offset % 2 ? 3 : 1,
    vwap: price * .94, highOfDay: price * 1.035, spreadPercent: .18 + offset * .03,
    catalystType: offset % 2 ? 5 : 6, catalystQuality: 4,
    aPlusScore: { totalScore: score, grade: score >= 85 ? 3 : 2, components: {
      catalyst: 16 + offset, relativeVolume: 11 + offset, gapMomentum: 8, float: 7,
      premarketVolume: 7 + offset, volumeAcceleration: 8, vwap: 10, setup: 5,
      resistanceRoom: 4, liquidity: 5
    }}, lastUpdated: now
  });
  return { sequence: -1, generatedAt: now, session: 3, marketHeat: 7.8, tickers: [
    ticker('VYNE', 8.42, 34.7, 92, 2), ticker('KAVL', 5.18, 26.3, 87, 1), ticker('PRZO', 3.76, 18.9, 81, 0)
  ]};
}

function demoNews(): NewsItem[] {
  return [{ id: -1, headline: 'Preview data: connect the market API to display live catalyst intelligence.',
    source: 'MomentumOS demo', symbols: ['VYNE', 'KAVL', 'PRZO'], catalystType: 6,
    catalystQuality: 4, publishedAt: new Date().toISOString() }];
}
