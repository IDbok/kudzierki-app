import { NgIf } from '@angular/common';
import { Component, inject, input, output, signal } from '@angular/core';
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
  protected readonly theme = signal<'light' | 'dark'>('light');

  readonly activeTab = input<'calendar' | 'cash'>('calendar');
  readonly tabChanged = output<'calendar' | 'cash'>();

  readonly loginRequested = output<void>();
  readonly logoutRequested = output<void>();

  constructor() {
    this.initTheme();
  }

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

  setTab(tab: 'calendar' | 'cash'): void {
    this.tabChanged.emit(tab);
  }

  toggleTheme(): void {
    const next = this.theme() === 'dark' ? 'light' : 'dark';
    this.theme.set(next);
    this.applyTheme(next);
  }

  private initTheme(): void {
    const stored = localStorage.getItem('theme');
    if (stored === 'dark' || stored === 'light') {
      this.theme.set(stored);
      this.applyTheme(stored);
      return;
    }

    const prefersDark =
      typeof window !== 'undefined' &&
      window.matchMedia &&
      window.matchMedia('(prefers-color-scheme: dark)').matches;

    const initial = prefersDark ? 'dark' : 'light';
    this.theme.set(initial);
    this.applyTheme(initial);
  }

  private applyTheme(theme: 'light' | 'dark'): void {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('theme', theme);
  }
}
