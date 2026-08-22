import { TestBed } from "@angular/core/testing";
import { provideRouter } from "@angular/router";
import { provideHttpClient } from "@angular/common/http";

import { HomeComponent } from "./home.component";

describe("HomeComponent", () => {
  it("should create", async () => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [provideRouter([]), provideHttpClient()],
    }).compileComponents();

    const fixture = TestBed.createComponent(HomeComponent);

    expect(fixture.componentInstance).toBeTruthy();
  });
});
