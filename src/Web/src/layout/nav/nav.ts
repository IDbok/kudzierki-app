import { NgIf } from '@angular/common';
import { Component, output, signal } from '@angular/core';

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
  protected readonly isAuthOpen = signal(false);

  readonly loginRequested = output<void>();

  openAuth(): void {
    this.isAuthOpen.set(true);
  }

  closeAuth(): void {
    this.isAuthOpen.set(false);
  }

  requestLogin(): void {
    this.loginRequested.emit();
  }
}
