import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export type WorkDayAssignmentResponse = {
  id: string;
  employeeId: string;
  portionType: number;
  portion: number;
  worked: boolean | null;
  note: string | null;
};

export type WorkDayResponse = {
  date: string;
  assignments: WorkDayAssignmentResponse[];
};

export type CreateWorkDayAssignmentRequest = {
  employeeId: string;
  portion: number;
  portionType: number;
  note?: string | null;
  worked?: boolean | null;
};

export type UpdateWorkDayAssignmentRequest = {
  portion?: number | null;
  portionType?: number | null;
  note?: string | null;
  worked?: boolean | null;
};

@Injectable({
  providedIn: 'root',
})
export class ScheduleService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;
  
  getWorkDays(from: string, to: string): Observable<WorkDayResponse[]> {
    return this.http.get<WorkDayResponse[]>(
      this.baseUrl + `schedule/workdays?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
      { withCredentials: true }
    );
  }

  upsertWorkDay(date: string): Observable<WorkDayResponse> {
    return this.http.put<WorkDayResponse>(
      this.baseUrl + `schedule/workdays/${encodeURIComponent(date)}`,
      {},
      { withCredentials: true }
    );
  }

  createAssignment(
    date: string,
    request: CreateWorkDayAssignmentRequest
  ): Observable<WorkDayAssignmentResponse> {
    return this.http.post<WorkDayAssignmentResponse>(
      this.baseUrl + `schedule/workdays/${encodeURIComponent(date)}/assignments`,
      request,
      { withCredentials: true }
    );
  }

  updateAssignment(
    assignmentId: string,
    request: UpdateWorkDayAssignmentRequest
  ): Observable<WorkDayAssignmentResponse> {
    return this.http.patch<WorkDayAssignmentResponse>(
      this.baseUrl + `schedule/assignments/${encodeURIComponent(assignmentId)}`,
      request,
      { withCredentials: true }
    );
  }

  deleteAssignment(assignmentId: string): Observable<void> {
    return this.http.delete<void>(
      this.baseUrl + `schedule/assignments/${encodeURIComponent(assignmentId)}`,
      { withCredentials: true }
    );
  }
}
