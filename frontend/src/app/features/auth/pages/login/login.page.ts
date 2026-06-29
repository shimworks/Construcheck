import { Component } from '@angular/core';
import { LoginFormComponent } from '../../components/login-form/login-form.component';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [LoginFormComponent],
  template: `<app-login-form />`,
})
export class LoginPageComponent {}
