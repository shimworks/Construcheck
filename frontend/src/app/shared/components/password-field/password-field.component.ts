import { Component, input } from '@angular/core';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-password-field',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './password-field.component.html',
})
export class PasswordFieldComponent {
  readonly label = input<string>('Senha:');
  readonly control = input.required<AbstractControl>();
  readonly placeholder = input<string>('••••••••');
  readonly inputId = input<string>('password');

  showPassword = false;

  toggleVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  showError(): boolean {
    const control = this.control();
    return control.invalid && (control.dirty || control.touched);
  }

  errorMessage(): string | null {
    const control = this.control();
    if (!this.showError()) {
      return null;
    }

    if (control.hasError('required')) {
      return 'Campo obrigatório.';
    }
    if (control.hasError('minlength')) {
      return 'Senha deve ter no mínimo 5 caracteres.';
    }
    if (control.hasError('passwordMismatch')) {
      return 'As senhas não coincidem.';
    }

    return 'Valor inválido.';
  }
}
