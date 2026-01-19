import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { LoginCreds, User } from '../../types/user';
import { tap } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;
  currentUser = signal<User | null>(null);
  
  login(creds: LoginCreds) {
    return this.http.post<User>(this.baseUrl + 'auth/login', creds,
      {withCredentials: true}
    ).pipe(
      tap(user => {
        if (user && user.accessToken) {
          localStorage.setItem('user', JSON.stringify(user));
          this.setCurrentUser(user);
        }
      })
    )
  }

  logout() {
    localStorage.removeItem('user');
    this.currentUser.set(null);
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
