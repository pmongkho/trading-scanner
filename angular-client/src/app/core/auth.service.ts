import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuthResponse {
  token: string;
  expiresAt: string;
  userId: number;
  email: string;
}

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly tokenKey = 'auth_token';

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/auth/login`, {
        email,
        password,
      })
      .pipe(tap((response) => this.setToken(response.token)));
  }

  register(
    email: string,
    password: string,
    firstName?: string,
    lastName?: string,
  ): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/auth/register`, {
        email,
        password,
        firstName,
        lastName,
      })
      .pipe(tap((response) => this.setToken(response.token)));
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    void this.router.navigate(['/login']);
  }

  private setToken(token: string): void {
    localStorage.setItem(this.tokenKey, token);
  }
}
