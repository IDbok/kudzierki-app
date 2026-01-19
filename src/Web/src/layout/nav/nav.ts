import { NgIf } from '@angular/common';
import { Component, inject, output, signal } from '@angular/core';
import { AccountService } from '../../core/services/account-service';

@Component({
  selector: 'app-nav',
  standalone: true,
  imports: [NgIf],
  templateUrl: './nav.html',
  styleUrl: './nav.css',
  host: {
    class: 'block'
  }
})
export class Nav {
  protected accountService = inject(AccountService);
  protected readonly isAuthOpen = signal(false);

  readonly loginRequested = output<void>();
  readonly logoutRequested = output<void>();

  openAuth(): void {
    this.isAuthOpen.set(true);
  }

  closeAuth(): void {
    this.isAuthOpen.set(false);
  }

  requestLogin(): void {
    this.loginRequested.emit();
  }

  requestLogout(): void {
    this.logoutRequested.emit();
  }
}
