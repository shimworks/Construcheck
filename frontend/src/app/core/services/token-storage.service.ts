import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  private readonly _accessToken = signal<string | null>(null);

  readonly accessToken = this._accessToken.asReadonly();

  setAccessToken(token: string | null): void {
    this._accessToken.set(token);
  }

  getAccessToken(): string | null {
    return this._accessToken();
  }
}
