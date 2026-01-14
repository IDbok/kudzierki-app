import { Component, inject, signal } from '@angular/core';
import { Nav } from '../layout/nav/nav';
import { AccountService } from '../core/services/account-service';
import { LoginCreds } from '../types/user';

@Component({
  selector: 'app-root',
  imports: [Nav],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected accountService = inject(AccountService);
  protected readonly title = signal('Kudzierki App');
  protected creds: LoginCreds = {
    email: 'admin@local',
    password: 'Admin123!'
  };

  login(){
    this.accountService.login(this.creds).subscribe({
      next: ()=> {
        this.creds = {
          email: '',
          password: ''
        };
        // this.router.navigateByUrl('/members');
      },
      error: error => {
        console.log(error);
      }
    })
  }
}
