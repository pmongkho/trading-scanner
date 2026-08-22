import { TestBed } from "@angular/core/testing";
import { provideRouter } from "@angular/router";
import { of } from "rxjs";

import { AuthService } from "../../core/auth.service";
import { LoginComponent } from "./login.component";

describe("LoginComponent", () => {
  it("should create", async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: { login: () => of(undefined) },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(LoginComponent);

    expect(fixture.componentInstance).toBeTruthy();
  });
});
