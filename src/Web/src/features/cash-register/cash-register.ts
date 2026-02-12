import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CashRegisterService } from '../../core/services/cash-register-service';
import { CashRegisterClosingResponse } from '../../types/cash-register';

@Component({
  selector: 'app-cash-register',
  imports: [CommonModule, FormsModule],
  templateUrl: './cash-register.html',
  styleUrl: './cash-register.css',
})
export class CashRegister implements OnInit {
  private cashRegisterService = inject(CashRegisterService);
  private destroyRef = inject(DestroyRef);

  protected readonly date = signal<string>(this.formatYmd(new Date()));
  protected readonly cashBalance = signal<string>('');
  protected readonly terminalIncome = signal<string>('');
  protected readonly comment = signal<string>('');

  protected readonly isSaving = signal(false);
  protected readonly lastClosing = signal<CashRegisterClosingResponse | null>(null);
  protected readonly recentClosings = signal<CashRegisterClosingResponse[]>([]);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadRecent();
  }

  save(): void {
    this.error.set(null);
    const cashBalance = this.parseDecimal(this.cashBalance());
    const terminalIncome = this.parseDecimal(this.terminalIncome());

    if (cashBalance === null || terminalIncome === null) {
      this.error.set('Enter valid numeric values for cash and terminal.');
      return;
    }

    this.isSaving.set(true);

    this.cashRegisterService
      .upsertClosing(this.date(), {
        cashBalanceFact: cashBalance,
        terminalIncomeFact: terminalIncome,
        comment: this.comment() || null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (closing) => {
          this.lastClosing.set(closing);
          this.loadRecent();
        },
        error: () => {
          this.error.set('Failed to save cash register closing.');
          this.isSaving.set(false);
        },
        complete: () => {
          this.isSaving.set(false);
        },
      });
  }

  loadRecent(): void {
    const today = new Date();
    const from = new Date(today);
    from.setDate(from.getDate() - 6);

    this.cashRegisterService
      .getClosings(this.formatYmd(from), this.formatYmd(today))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (closings) => {
          this.recentClosings.set(closings);
        },
        error: () => {
          this.error.set('Failed to load recent closings.');
        },
      });
  }

  private parseDecimal(value: string | number): number | null {
    if (value === null || value === undefined || value === '') return null;
    const normalized = typeof value === 'string' ? value.replace(',', '.') : String(value);
    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
  }

  private formatYmd(date: Date): string {
    const yyyy = String(date.getFullYear());
    const mm = String(date.getMonth() + 1).padStart(2, '0');
    const dd = String(date.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  }
}
