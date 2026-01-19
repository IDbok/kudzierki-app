import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { Employee } from '../../types/employee';

@Injectable({
  providedIn: 'root',
})
export class EmployeeService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  getEmployees() {
    return this.http.get<Employee[]>(this.baseUrl + 'employees', 
      { withCredentials: true });
  }

  getAdmins() {
    return this.http.get<Employee[]>(this.baseUrl + 'employees?position=1', 
      { withCredentials: true });
  }

  getCleaners() {
    return this.http.get<Employee[]>(this.baseUrl + 'employees?position=3',
      { withCredentials: true });
  }
}
