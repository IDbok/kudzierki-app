import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { LoginCreds, User } from '../../types/user';
import { tap } from 'rxjs/internal/operators/tap';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  private http = inject(HttpClient);
  currentUser = signal<User | null>(null);
  private baseUrl = environment.apiUrl;
  
  login(creds: LoginCreds) {
    console.log('Logging in with creds:', creds);
    return this.http.post<User>(this.baseUrl + 'auth/login', creds,
      {withCredentials: true}
    ).pipe(
      tap(user => {
        if (user && user.accessToken) { 
          console.log('Login successful, received user:', user);
          this.setCurrentUser(user); 
          // this.startTokenRefreshTimer();
        }
        else{
          console.error('Login failed: No access token received');
        }
      })
    )
  }

  private setCurrentUser(user: User) {
    user.roles = this.getRolesFromToken(user.accessToken);
    this.currentUser.set(user);
  }

  private getRolesFromToken(token: string): string[] {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return Array.isArray(payload.role) ? payload.role : [payload.role];
  }
}
