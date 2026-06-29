import { Component, input } from '@angular/core';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-form-field',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './form-field.component.html',
})
export class FormFieldComponent {
  readonly label = input.required<string>();
  readonly control = input.required<AbstractControl>();
  readonly type = input<string>('text');
  readonly placeholder = input<string>('');
  readonly inputId = input.required<string>();

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
    if (control.hasError('email')) {
      return 'Informe um e-mail válido.';
    }
    if (control.hasError('minlength')) {
      return 'Senha deve ter no mínimo 5 caracteres.';
    }

    return 'Valor inválido.';
  }
}
