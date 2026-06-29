import { computed, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { jwtDecode } from 'jwt-decode';
import { catchError, map, Observable, of, tap } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { TokenStorageService } from '../../../core/services/token-storage.service';
import { AuthResponse } from '../models/auth-response.model';
import { LoginRequest } from '../models/login.request';
import { RegisterRequest } from '../models/register.request';
import { User, UserRole } from '../models/user.model';

interface JwtPayload {
  sub: string;
  email: string;
  role?: string | string[];
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string | string[];
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = `${environment.apiUrl}/api/auth`;
  private readonly _currentUser = signal<User | null>(null);

  readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this._currentUser() !== null);

  constructor(
    private readonly http: HttpClient,
    private readonly tokenStorage: TokenStorageService,
  ) {}

  initializeSession(): Observable<User | null> {
    return this.refresh().pipe(
      catchError(() => {
        this.clearSession();
        return of(null);
      }),
    );
  }

  login(request: LoginRequest): Observable<User> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, request).pipe(
      tap((response) => this.applyAccessToken(response.accessToken)),
      map(() => this._currentUser()!),
    );
  }

  register(request: RegisterRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/register`, request);
  }

  refresh(): Observable<User> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/refresh`, {}).pipe(
      tap((response) => this.applyAccessToken(response.accessToken)),
      map(() => this._currentUser()!),
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/logout`, {}).pipe(
      tap(() => this.clearSession()),
    );
  }

  private applyAccessToken(accessToken: string): void {
    this.tokenStorage.setAccessToken(accessToken);
    this._currentUser.set(this.decodeUser(accessToken));
  }

  private clearSession(): void {
    this.tokenStorage.setAccessToken(null);
    this._currentUser.set(null);
  }

  private decodeUser(accessToken: string): User {
    const payload = jwtDecode<JwtPayload>(accessToken);
    const rolesClaim =
      payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? payload.role;

    const roles = Array.isArray(rolesClaim)
      ? (rolesClaim as UserRole[])
      : rolesClaim
        ? [rolesClaim as UserRole]
        : [];

    return {
      id: payload.sub,
      email: payload.email,
      roles,
    };
  }
}
