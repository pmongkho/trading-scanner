import { Component } from '@angular/core';

interface ScannerRow {
  symbol: string; score: number; price: number; change: number; gap: number;
  rvol: number; float: string; volume: string; momentum: string; setup: string;
}

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
})
export class HomeComponent {
  readonly tabs = ['A+ Now', 'Top Gappers', 'HOD Momentum', 'Volume Surge', 'VWAP Reclaim', 'Premarket High Break', '15-Min ORB', 'First Pullback', 'Halts'];
  readonly rows: ScannerRow[] = [
    { symbol: 'KLYN', score: 94, price: 7.82, change: 68.4, gap: 41.2, rvol: 12.8, float: '4.2M', volume: '18.4M', momentum: 'Strong', setup: 'PM High Break' },
    { symbol: 'VTRX', score: 88, price: 4.16, change: 37.1, gap: 22.8, rvol: 8.4, float: '7.9M', volume: '9.7M', momentum: 'Accelerating', setup: 'VWAP Reclaim' },
    { symbol: 'ARQO', score: 81, price: 12.43, change: 24.7, gap: 18.5, rvol: 5.9, float: '11.3M', volume: '6.2M', momentum: 'Building', setup: '15-Min ORB' },
    { symbol: 'NMBL', score: 76, price: 2.91, change: 19.8, gap: 14.2, rvol: 3.6, float: '16.8M', volume: '3.1M', momentum: 'Building', setup: 'Waiting' },
  ];
  selected = this.rows[0];
  readonly scores = [
    ['Catalyst', 20, 20], ['Relative volume', 15, 15], ['Gap momentum', 10, 10],
    ['Float', 10, 10], ['Premarket volume', 8, 10], ['Volume acceleration', 10, 10],
    ['VWAP structure', 9, 10], ['Setup quality', 4, 5], ['Resistance room', 4, 5], ['Liquidity', 4, 5],
  ] as const;
}
