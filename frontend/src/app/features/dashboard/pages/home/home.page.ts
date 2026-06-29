import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../../auth/services/auth.service';
import { HealthService } from '../../../../core/services/health.service';
import { AppHeaderComponent } from '../../../../shared/components/app-header/app-header.component';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [AppHeaderComponent],
  templateUrl: './home.page.html',
})
export class HomePageComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly healthService = inject(HealthService);
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  readonly healthStatus = signal<string | null>(null);
  readonly healthError = signal<string | null>(null);

  ngOnInit(): void {
    this.healthService.check().subscribe({
      next: (response) => this.healthStatus.set(`${response.status} — ${response.timestamp}`),
      error: () => this.healthError.set('Não foi possível consultar /api/health'),
    });
  }

  logout(): void {
    this.authService.logout().subscribe({
      next: () => this.router.navigate(['/auth/login']),
    });
  }
}
