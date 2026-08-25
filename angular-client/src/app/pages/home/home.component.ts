import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ScannerStore, TickerState } from '../../core/scanner.store';

@Component({ selector: 'app-home', imports: [CurrencyPipe, DatePipe, DecimalPipe], templateUrl: './home.component.html', styleUrl: './home.component.css' })
export class HomeComponent implements OnInit, OnDestroy {
  readonly store = inject(ScannerStore);
  readonly tabs = ['A+ Now', 'Top Gappers', 'HOD Momentum', 'Volume Surge', 'VWAP Reclaim', 'Premarket High Break', '15-Min ORB', 'First Pullback', 'Halts'];
  readonly selectedSymbol = signal<string | null>(null);
  readonly selected = computed(() => this.store.tickers().find(x => x.symbol === this.selectedSymbol()) ?? this.store.tickers()[0] ?? null);
  readonly selectedNews = computed(() => this.store.news().find(x => this.selected()?.symbol && x.symbols.includes(this.selected()!.symbol)) ?? this.store.news()[0]);
  readonly scoreKeys: [keyof TickerState['aPlusScore']['components'], string, number][] = [['catalyst','Catalyst',20],['relativeVolume','Relative volume',15],['gapMomentum','Gap momentum',10],['float','Float',10],['premarketVolume','Premarket volume',10],['volumeAcceleration','Volume acceleration',10],['vwap','VWAP structure',10],['setup','Setup quality',5],['resistanceRoom','Resistance room',5],['liquidity','Liquidity',5]];
  readonly chartBars = computed(() => {
    const stock = this.selected();
    if (!stock) return [];
    return this.scoreKeys.map(([key, label, max]) => ({ label, value: stock.aPlusScore.components[key], height: Math.max(4, stock.aPlusScore.components[key] / max * 100) }));
  });
  readonly emptyMessage = computed(() => {
    if (this.store.connection() === 'connecting') return 'Hydrating dashboard…';
    if (this.store.snapshot()?.session === 0)
      return 'Market closed · showing the latest available snapshot when Alpaca has data.';
    if (this.store.connection() !== 'live')
      return 'Market data is unavailable · reconnecting to Alpaca…';
    return 'No symbols currently match scanner filters.';
  });
  ngOnInit() { void this.store.start(); }
  ngOnDestroy() { this.store.stop(); }
  choose(row: TickerState) { this.selectedSymbol.set(row.symbol); }
  compact(value: number | null) { return value == null ? '—' : Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 }).format(value); }
  momentum(value: number) { return ['Dormant','Building','Accelerating','Strong','Extended','Fading'][value] ?? 'Unknown'; }
  setup(value: number) { return ['Waiting','First Pullback','VWAP Hold','VWAP Reclaim','15-Min ORB','PM High Break','HOD Break','Extended','Failed'][value] ?? 'Unknown'; }
  catalyst(value: number) { return ['FDA','Clinical Trial','Acquisition','Buyout','Major Contract','Earnings','Partnership','Government Contract','Patent','Analyst Action','Corporate Update','Offering','Dilution','Reverse Split','Unknown','None'][value] ?? 'Unknown'; }
  session(value?: number) { return ['CLOSED','PREMARKET','OPENING RANGE','REGULAR','AFTER HOURS'][value ?? 0]; }
}
