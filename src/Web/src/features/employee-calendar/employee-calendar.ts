import { AfterViewInit, Component, DestroyRef, inject, NgZone, OnInit, signal, ViewChild } from '@angular/core';
import { FullCalendarComponent, FullCalendarModule } from '@fullcalendar/angular';
import { CalendarOptions, DatesSetArg, EventInput } from '@fullcalendar/core';
import dayGridPlugin from '@fullcalendar/daygrid';
import interactionPlugin from '@fullcalendar/interaction';

import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  Subject,
  debounceTime,
  distinctUntilChanged,
  forkJoin,
  of,
  switchMap,
  tap,
  type Observable,
} from 'rxjs';

import ruLocale from '@fullcalendar/core/locales/ru';
import { EmployeeService } from '../../core/services/employee-service';
import { Employee } from '../../types/employee';
import {
  CreateWorkDayAssignmentRequest,
  ScheduleService,
  UpdateWorkDayAssignmentRequest,
  WorkDayAssignmentResponse,
  WorkDayResponse,
} from '../../core/services/schedule-service';

import {
  AdminAssignmentDraft,
  CleaningAssignmentDraft,
  DayModalSavePayload,
  EmployeeCalendarDayModal,
} from './day-modal/employee-calendar-day-modal';

type EmployeeEventInput = EventInput & {
  extendedProps?: {
    employeeId: Employee['id'];
  };
};

@Component({
  selector: 'app-employee-calendar',
  imports: [FullCalendarModule, EmployeeCalendarDayModal],
  templateUrl: './employee-calendar.html',
  styleUrl: './employee-calendar.css',
})
export class EmployeeCalendar implements OnInit, AfterViewInit {
  @ViewChild(FullCalendarComponent) private calendarComponent?: FullCalendarComponent;

  private employeeService = inject(EmployeeService);
  private scheduleService = inject(ScheduleService);
  private destroyRef = inject(DestroyRef);
  private ngZone = inject(NgZone);

  private employees = new Map<Employee['id'], Employee>();
  private colorByEmployeeId = new Map<Employee['id'], string>();

  protected readonly isDayModalOpen = signal(false);
  protected readonly selectedDateStr = signal<string | null>(null);
  protected readonly admins = signal<Employee[]>([]);
  protected readonly cleaners = signal<Employee[]>([]);
  protected readonly allEmployees = signal<Employee[]>([]);

  protected readonly existingAdminAssignments = signal<AdminAssignmentDraft[]>([]);
  protected readonly existingCleaningAssignment = signal<CleaningAssignmentDraft | null>(null);

  private events: EmployeeEventInput[] = [];

  private currentWorkDays: WorkDayResponse[] = [];
  private currentRange: { from: string; to: string } | null = null;

  private pendingEvents: EmployeeEventInput[] | null = null;
  private eventsUpdateSeq = 0;
  private lastEventSignatureById = new Map<string, string>();

  private readonly visibleRange$ = new Subject<{ from: string; to: string }>();
  private pendingVisibleRange: { from: string; to: string } | null = null;

  calendarOptions: CalendarOptions = {
    initialView: 'dayGridMonth',
    plugins: [dayGridPlugin, interactionPlugin],
    locale: ruLocale,
    firstDay: 1,
    height: 'auto',
    events: this.events,
    showNonCurrentDates: true,

    datesSet: (arg) => {
      const range = this.getVisibleInclusiveRange(arg);
      this.pendingVisibleRange = range;
      this.ngZone.run(() => {
        this.visibleRange$.next(range);
      });
    },

    dateClick: (arg) => {
      this.ngZone.run(() => {
        this.openDayModal(arg.dateStr);
      });
    },

    eventClick: (arg) => {
      const dateStr = (arg.event.startStr ?? '').slice(0, 10);
      this.ngZone.run(() => {
        this.openDayModal(dateStr);
      });
    },
  };

  ngAfterViewInit(): void {
    if (this.pendingEvents) {
      this.setCalendarEvents(this.pendingEvents);
    }
  }
  
  ngOnInit(): void {
    this.visibleRange$
      .pipe(
        debounceTime(75),
        distinctUntilChanged((a, b) => a.from === b.from && a.to === b.to),
        tap((range) => {
          this.currentRange = range;
        }),
        switchMap((range) => this.scheduleService.getWorkDays(range.from, range.to)),
        tap((workDays) => {
          this.currentWorkDays = workDays;
          this.events = this.workDaysToEvents(workDays);
          this.setCalendarEvents(this.events);

          const selected = this.selectedDateStr();
          if (selected && this.isDayModalOpen()) {
            this.syncModalStateFromCurrentData(selected);
          }
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        error: (err: unknown) => {
          console.error('Failed to load work days', err);
        },
      });

    if (this.pendingVisibleRange) {
      this.visibleRange$.next(this.pendingVisibleRange);
    }

    this.employeeService.getEmployees().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((employees) => {
      this.allEmployees.set(employees);
      this.employees = new Map(
        employees.map((e) => [e.id, e])
      );

      this.admins.set(employees.filter((e) => e.position === 1));
      this.cleaners.set(employees.filter((e) => e.position === 3));

      if (this.currentWorkDays.length > 0) {
        this.events = this.workDaysToEvents(this.currentWorkDays);
        this.setCalendarEvents(this.events);
      }
    });
  }

  openDayModal(dateStr: string): void {
    this.selectedDateStr.set(dateStr);
    this.syncModalStateFromCurrentData(dateStr);
    this.isDayModalOpen.set(true);
  }

  closeDayModal(): void {
    this.isDayModalOpen.set(false);
  }
  saveAssignmentsForSelectedDate(payload: DayModalSavePayload): void {
    const dateStr = payload.dateStr;

    const desired = payload.adminAssignments;
    const existing = this.existingAdminAssignments();

    const desiredByEmployeeId = new Map(desired.map((x) => [x.employeeId, x] as const));

    const ops: Observable<unknown>[] = [];

    for (const ex of existing) {
      if (!desiredByEmployeeId.has(ex.employeeId) && ex.assignmentId) {
        ops.push(this.scheduleService.deleteAssignment(ex.assignmentId));
      }
    }

    for (const d of desired) {
      const { portionType, portion } = this.toPortion(d.halfShift);

      if (d.assignmentId) {
        const old = existing.find((x) => x.employeeId === d.employeeId);
        if (old && old.halfShift !== d.halfShift) {
          const req: UpdateWorkDayAssignmentRequest = {
            portionType,
            portion,
          };
          ops.push(this.scheduleService.updateAssignment(d.assignmentId, req));
        }
      } else {
        const req: CreateWorkDayAssignmentRequest = {
          employeeId: d.employeeId,
          portionType,
          portion,
          note: null,
          worked: null,
        };
        ops.push(this.scheduleService.createAssignment(dateStr, req));
      }
    }

    // Cleaning assignment sync
    const existingCleaning = this.existingCleaningAssignment();
    const wantCleaning = payload.cleaning.enabled;
    const desiredCleanerId = payload.cleaning.cleanerId;

    if (!wantCleaning) {
      if (existingCleaning?.assignmentId) {
        ops.push(this.scheduleService.deleteAssignment(existingCleaning.assignmentId));
      }
    } else {
      const cleanerIdToUse = desiredCleanerId ?? (this.cleaners().length === 1 ? this.cleaners()[0]!.id : null);
      if (cleanerIdToUse) {
        if (existingCleaning?.assignmentId) {
          if (existingCleaning.employeeId !== cleanerIdToUse) {
            ops.push(this.scheduleService.deleteAssignment(existingCleaning.assignmentId));
            ops.push(
              this.scheduleService.createAssignment(dateStr, {
                employeeId: cleanerIdToUse,
                portionType: 0,
                portion: 1.0,
                note: 'Уборка',
                worked: null,
              })
            );
          }
        } else {
          ops.push(
            this.scheduleService.createAssignment(dateStr, {
              employeeId: cleanerIdToUse,
              portionType: 0,
              portion: 1.0,
              note: 'Уборка',
              worked: null,
            })
          );
        }
      }
    }

    const range = this.currentRange;
    const refresh$: Observable<WorkDayResponse[]> = range
      ? this.scheduleService.getWorkDays(range.from, range.to)
      : this.scheduleService.getWorkDays(dateStr, dateStr);

    const ops$ = ops.length ? forkJoin(ops) : of([] as unknown[]);

    ops$
      .pipe(
        switchMap(() => refresh$),
        tap((workDays: WorkDayResponse[]) => {
          this.currentWorkDays = workDays;
          this.events = this.workDaysToEvents(workDays);
          this.setCalendarEvents(this.events);

          this.syncModalStateFromCurrentData(dateStr);
        })
      )
      .subscribe({
        next: () => {
          this.isDayModalOpen.set(false);
        },
        error: (err: unknown) => {
          console.error('Failed to save assignments', err);
        },
      });
  }

  private loadWorkDaysForCurrentMonth(arg: DatesSetArg): void {
    // Deprecated: use visible range stream via `datesSet`.
    void arg;
  }

  private workDaysToEvents(workDays: WorkDayResponse[]): EmployeeEventInput[] {
    const events: EmployeeEventInput[] = [];

    for (const workDay of workDays) {
      for (const assignment of workDay.assignments ?? []) {
        const employeeId = assignment.employeeId as Employee['id'];
        if (!this.isDisplayedEmployee(employeeId)) continue;

        const color = this.isCleanerEmployee(employeeId) ? '#16a34a' : this.getEmployeeColor(employeeId);

        events.push({
          id: assignment.id,
          title: this.buildEventTitle(employeeId, assignment.note, assignment.portionType, assignment.portion),
          start: workDay.date,
          allDay: true,
          backgroundColor: color,
          borderColor: color,
          textColor: '#fff',
          extendedProps: {
            employeeId,
          },
        });
      }
    }

    return events;
  }

  private buildEventTitle(
    employeeId: Employee['id'],
    note: string | null | undefined,
    portionType: number | null | undefined,
    portion: number | null | undefined
  ): string {
    const employee = this.employees.get(employeeId);
    if (!employee) return 'Неизвестный сотрудник';
    note; // void note;
    // const noteSuffix = note ? ` — ${note}` : '';

    const portionSuffix = portion != null && portion !== 1 ? ` (${portion})` : '';
    void portionType; // todo: use portionType if needed in future

    return `${employee.firstName}${portionSuffix}`;
  }

  private syncModalStateFromCurrentData(dateStr: string): void {
    const day = this.currentWorkDays.find((d) => (d.date ?? '').slice(0, 10) === dateStr);
    const allAssignments = day?.assignments ?? [];

    const adminAssignments = allAssignments.filter((a) => this.isAdminEmployee(a.employeeId));
    const existingDraft = adminAssignments.map((a) => this.assignmentToDraft(a));

    const cleaningAssignment = allAssignments.find((a) => this.isCleanerEmployee(a.employeeId));
    const cleaningDraft = cleaningAssignment
      ? {
          employeeId: cleaningAssignment.employeeId,
          assignmentId: cleaningAssignment.id,
        }
      : null;

    this.existingAdminAssignments.set(existingDraft);
    this.existingCleaningAssignment.set(cleaningDraft);
  }

  private assignmentToDraft(a: WorkDayAssignmentResponse): AdminAssignmentDraft {
    return {
      employeeId: a.employeeId,
      assignmentId: a.id,
      halfShift: this.isHalfShift(a.portionType, a.portion),
    };
  }

  private isHalfShift(portionType: number | null | undefined, portion: number | null | undefined): boolean {
    return portionType === 1 || portion === 0.5;
  }

  private toPortion(halfShift: boolean): { portionType: number; portion: number } {
    return halfShift ? { portionType: 1, portion: 0.5 } : { portionType: 0, portion: 1.0 };
  }

  private isAdminEmployee(employeeId: Employee['id']): boolean {
    const employee = this.employees.get(employeeId);
    if (employee) return employee.position === 1;
    return this.admins().some((a) => a.id === employeeId);
  }

  private isCleanerEmployee(employeeId: Employee['id']): boolean {
    const employee = this.employees.get(employeeId);
    if (employee) return employee.position === 3;
    return this.cleaners().some((c) => c.id === employeeId);
  }

  private isDisplayedEmployee(employeeId: Employee['id']): boolean {
    return this.isAdminEmployee(employeeId) || this.isCleanerEmployee(employeeId);
  }

  private getEmployeeColor(employeeId: Employee['id']): string {
    const cached = this.colorByEmployeeId.get(employeeId);
    if (cached) return cached;

    const hue = this.hashToPositiveInt(employeeId) % 360;
    const color = `hsl(${hue} 70% 40%)`;
    this.colorByEmployeeId.set(employeeId, color);
    return color;
  }

  private hashToPositiveInt(value: string): number {
    let hash = 0;
    for (let i = 0; i < value.length; i++) {
      hash = (hash * 31 + value.charCodeAt(i)) | 0;
    }
    return Math.abs(hash);
  }

  private formatYmdLocal(date: Date): string {
    const yyyy = String(date.getFullYear());
    const mm = String(date.getMonth() + 1).padStart(2, '0');
    const dd = String(date.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  }

  private getVisibleInclusiveRange(arg: DatesSetArg): { from: string; to: string } {
    // For dayGridMonth, `activeStart/activeEnd` represent the full rendered grid,
    // including spillover days from соседних месяцев (when showNonCurrentDates=true).
    const from = this.formatYmdLocal(arg.view.activeStart);
    const inclusiveEnd = new Date(arg.view.activeEnd);
    inclusiveEnd.setDate(inclusiveEnd.getDate() - 1);
    const to = this.formatYmdLocal(inclusiveEnd);
    return { from, to };
  }

  private setCalendarEvents(events: EmployeeEventInput[]): void {
    this.pendingEvents = events;
    const api = this.calendarComponent?.getApi();
    if (!api) {
      // First render fallback.
      this.calendarOptions = {
        ...this.calendarOptions,
        events,
      };
      return;
    }

    const seq = ++this.eventsUpdateSeq;
    requestAnimationFrame(() => {
      if (seq !== this.eventsUpdateSeq) return;

      const nextById = new Map<string, EmployeeEventInput>();
      for (const e of events) {
        if (!e.id) continue;
        nextById.set(String(e.id), e);
      }

      api.batchRendering(() => {
        // Remove events not present anymore.
        for (const existing of api.getEvents()) {
          const id = existing.id;
          if (!nextById.has(id)) {
            existing.remove();
            this.lastEventSignatureById.delete(id);
          }
        }

        // Add/update changed events only.
        for (const [id, next] of nextById) {
          const nextSig = this.eventSignature(next);
          const prevSig = this.lastEventSignatureById.get(id);
          if (prevSig === nextSig) continue;

          const existing = api.getEventById(id);
          if (existing) existing.remove();

          api.addEvent(next);
          this.lastEventSignatureById.set(id, nextSig);
        }
      });
    });
  }

  private eventSignature(e: EmployeeEventInput): string {
    const start = typeof e.start === 'string' ? e.start : e.start instanceof Date ? e.start.toISOString() : '';
    const color = `${e.backgroundColor ?? ''}|${e.borderColor ?? ''}|${e.textColor ?? ''}`;
    const employeeId = (e.extendedProps as { employeeId?: string } | undefined)?.employeeId ?? '';
    return `${e.id ?? ''}|${e.title ?? ''}|${start}|${e.allDay ? '1' : '0'}|${color}|${employeeId}`;
  }
}

