import { TestBed } from "@angular/core/testing";
import { provideRouter } from "@angular/router";
import { provideHttpClient } from "@angular/common/http";

import { HomeComponent } from "./home.component";
import { ScannerStore } from "../../core/scanner.store";

describe("HomeComponent", () => {
  it("should create", async () => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [provideRouter([]), provideHttpClient()],
    }).compileComponents();

    const fixture = TestBed.createComponent(HomeComponent);

    expect(fixture.componentInstance).toBeTruthy();
  });

  it("explains an empty scanner while the market is closed", async () => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [provideRouter([]), provideHttpClient()],
    }).compileComponents();

    const fixture = TestBed.createComponent(HomeComponent);
    const store = TestBed.inject(ScannerStore);
    store.snapshot.set({ sequence: 0, generatedAt: new Date().toISOString(), session: 0, marketHeat: 0, tickers: [] });
    store.connection.set("degraded");

    expect(fixture.componentInstance.emptyMessage()).toContain("Market closed");
  });
});
