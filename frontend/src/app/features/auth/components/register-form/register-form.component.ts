import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { FormFieldComponent } from '../../../../shared/components/form-field/form-field.component';
import { PasswordFieldComponent } from '../../../../shared/components/password-field/password-field.component';

function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;

  if (password !== confirmPassword) {
    control.get('confirmPassword')?.setErrors({ passwordMismatch: true });
    return { passwordMismatch: true };
  }

  return null;
}

@Component({
  selector: 'app-register-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, FormFieldComponent, PasswordFieldComponent],
  templateUrl: './register-form.component.html',
})
export class RegisterFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group(
    {
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordMatchValidator },
  );

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const { email, password } = this.form.getRawValue();

    this.authService.register({ email, password }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.successMessage.set('Conta criada com sucesso! Redirecionando para o login...');
        setTimeout(() => this.router.navigate(['/auth/login']), 1500);
      },
      error: (error: { message: string }) => {
        this.isSubmitting.set(false);
        this.errorMessage.set(error.message);
      },
    });
  }
}
