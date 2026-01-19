import { Component, inject, OnInit, signal } from '@angular/core';
import { Nav } from '../layout/nav/nav';
import { AccountService } from '../core/services/account-service';
import { LoginCreds, User } from '../types/user';
import { EmployeeCalendar } from '../features/employee-calendar/employee-calendar';

@Component({
  selector: 'app-root',
  imports: [Nav, EmployeeCalendar],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  protected accountService = inject(AccountService);
  protected readonly title = signal('Kudzierki App');
  protected creds: LoginCreds = {
    email: 'admin@local',
    password: 'Admin123!'
  };

  ngOnInit(): void {
    this.setCurrentUser();
  }
  
  setCurrentUser(){
    const userJson = localStorage.getItem('user');
    if(userJson){
      const user: User = JSON.parse(userJson);
      this.accountService.currentUser.set(user);
    }
  }

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
        console.error('Login failed', error);
      }
    })
  }
}
