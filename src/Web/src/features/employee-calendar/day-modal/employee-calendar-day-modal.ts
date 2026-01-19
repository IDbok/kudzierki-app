import { CommonModule } from '@angular/common';
import { Component, effect, input, output, signal } from '@angular/core';

import { Employee } from '../../../types/employee';

export type AdminAssignmentDraft = {
  employeeId: Employee['id'];
  assignmentId?: string;
  halfShift: boolean;
};

export type CleaningAssignmentDraft = {
  employeeId: Employee['id'];
  assignmentId: string;
};

export type DayModalSavePayload = {
  dateStr: string;
  adminAssignments: AdminAssignmentDraft[];
  cleaning: {
    enabled: boolean;
    cleanerId: Employee['id'] | null;
  };
};

@Component({
  selector: 'app-employee-calendar-day-modal',
  imports: [CommonModule],
  templateUrl: './employee-calendar-day-modal.html',
  styleUrl: './employee-calendar-day-modal.css',
})
export class EmployeeCalendarDayModal {
  readonly open = input<boolean>(false);
  readonly dateStr = input<string | null>(null);

  readonly employees = input<Employee[]>([]);
  readonly admins = input<Employee[]>([]);
  readonly cleaners = input<Employee[]>([]);

  readonly existingAdminAssignments = input<AdminAssignmentDraft[]>([]);
  readonly existingCleaningAssignment = input<CleaningAssignmentDraft | null>(null);

  readonly closed = output<void>();
  readonly saveRequested = output<DayModalSavePayload>();

  protected readonly isEditMode = signal(false);

  protected readonly draftAdminAssignments = signal<AdminAssignmentDraft[]>([]);

  protected readonly draftCleaningEnabled = signal(false);
  protected readonly draftCleanerId = signal<Employee['id'] | null>(null);

  constructor() {
    effect(() => {
      if (!this.open()) return;

      const dateStr = this.dateStr();
      if (!dateStr) return;

      this.syncStateFromInputs();
    });
  }

  requestClose(): void {
    this.closed.emit();
  }

  enterEditMode(): void {
    this.isEditMode.set(true);
    this.draftAdminAssignments.set(this.existingAdminAssignments().map((a) => ({ ...a })));

    const existingCleaning = this.existingCleaningAssignment();
    if (existingCleaning) {
      this.draftCleaningEnabled.set(true);
      this.draftCleanerId.set(existingCleaning.employeeId);
    } else {
      this.draftCleaningEnabled.set(false);
      this.draftCleanerId.set(null);
    }
  }

  toggleCleaningEnabled(checked: boolean): void {
    this.draftCleaningEnabled.set(checked);
    if (!checked) {
      this.draftCleanerId.set(null);
      return;
    }

    const cleaners = this.cleaners();
    if (cleaners.length === 1) {
      this.draftCleanerId.set(cleaners[0]!.id);
    }
  }

  onCleanerSelected(employeeId: Employee['id'] | null): void {
    this.draftCleanerId.set(employeeId);
  }

  canSave(): boolean {
    if (!this.isEditMode()) return false;
    if (!this.draftCleaningEnabled()) return true;

    const cleaners = this.cleaners();
    if (cleaners.length <= 1) return true;
    return this.draftCleanerId() != null;
  }

  toggleAdminSelected(employeeId: Employee['id'], checked: boolean): void {
    const existing = this.draftAdminAssignments();

    if (checked) {
      if (existing.some((x) => x.employeeId === employeeId)) return;

      const assignmentId = this.existingAdminAssignments().find((x) => x.employeeId === employeeId)?.assignmentId;
      this.draftAdminAssignments.set([
        ...existing,
        {
          employeeId,
          assignmentId,
          halfShift: false,
        },
      ]);
      return;
    }

    this.draftAdminAssignments.set(existing.filter((x) => x.employeeId !== employeeId));
  }

  setAdminHalfShift(employeeId: Employee['id'], halfShift: boolean): void {
    this.draftAdminAssignments.set(
      this.draftAdminAssignments().map((x) => (x.employeeId === employeeId ? { ...x, halfShift } : x))
    );
  }

  isAdminSelected(employeeId: Employee['id']): boolean {
    return this.draftAdminAssignments().some((x) => x.employeeId === employeeId);
  }

  getAdminHalfShift(employeeId: Employee['id']): boolean {
    return this.draftAdminAssignments().find((x) => x.employeeId === employeeId)?.halfShift ?? false;
  }

  requestSave(): void {
    const dateStr = this.dateStr();
    if (!dateStr) return;

    if (!this.canSave()) return;

    this.saveRequested.emit({
      dateStr,
      adminAssignments: this.draftAdminAssignments(),
      cleaning: {
        enabled: this.draftCleaningEnabled(),
        cleanerId: this.draftCleanerId(),
      },
    });
  }

  employeeLabel(employeeId: Employee['id']): string {
    const employee = this.employees().find((e) => e.id === employeeId);
    if (employee) return `${employee.firstName} ${employee.lastName}`.trim();

    const fromAdmins = this.admins().find((a) => a.id === employeeId);
    if (fromAdmins) return `${fromAdmins.firstName} ${fromAdmins.lastName}`.trim();

    const fromCleaners = this.cleaners().find((c) => c.id === employeeId);
    if (fromCleaners) return `${fromCleaners.firstName} ${fromCleaners.lastName}`.trim();

    return 'Неизвестный сотрудник';
  }

  private syncStateFromInputs(): void {
    const existingAdmins = this.existingAdminAssignments();
    const existingCleaning = this.existingCleaningAssignment();

    const hasAnyAssignments = existingAdmins.length > 0 || existingCleaning != null;

    if (!hasAnyAssignments) {
      this.isEditMode.set(true);
      this.draftAdminAssignments.set([]);
      this.draftCleaningEnabled.set(false);
      this.draftCleanerId.set(null);
      return;
    }

    this.isEditMode.set(false);
    this.draftAdminAssignments.set(existingAdmins.map((x) => ({ ...x })));

    if (existingCleaning) {
      this.draftCleaningEnabled.set(true);
      this.draftCleanerId.set(existingCleaning.employeeId);
    } else {
      this.draftCleaningEnabled.set(false);
      this.draftCleanerId.set(null);
    }
  }
}
