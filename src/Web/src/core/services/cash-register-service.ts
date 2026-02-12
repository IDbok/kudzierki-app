import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { CashRegisterClosingRequest, CashRegisterClosingResponse } from '../../types/cash-register';

@Injectable({
  providedIn: 'root',
})
export class CashRegisterService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  getClosings(from: string, to: string) {
    return this.http.get<CashRegisterClosingResponse[]>(
      `${this.baseUrl}cash-register/closings?from=${from}&to=${to}`,
      { withCredentials: true }
    );
  }

  getClosing(date: string) {
    return this.http.get<CashRegisterClosingResponse>(
      `${this.baseUrl}cash-register/closings/${date}`,
      { withCredentials: true }
    );
  }

  upsertClosing(date: string, request: CashRegisterClosingRequest) {
    return this.http.put<CashRegisterClosingResponse>(
      `${this.baseUrl}cash-register/closings/${date}`,
      request,
      { withCredentials: true }
    );
  }
}
