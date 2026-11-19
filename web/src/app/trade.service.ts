import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { BlotterSummary, BookTradeRequest, Trade, TradeAction, TradeStatus } from './trade.models';

@Injectable({ providedIn: 'root' })
export class TradeService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/trades';

  list(status?: TradeStatus | '', counterparty?: string): Observable<Trade[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (counterparty?.trim()) params = params.set('counterparty', counterparty.trim());
    return this.http.get<Trade[]>(this.base, { params }).pipe(catchError(toMessage));
  }

  summary(): Observable<BlotterSummary> {
    return this.http.get<BlotterSummary>(`${this.base}/summary`).pipe(catchError(toMessage));
  }

  book(request: BookTradeRequest): Observable<Trade> {
    return this.http.post<Trade>(this.base, request).pipe(catchError(toMessage));
  }

  transition(id: number, action: TradeAction): Observable<Trade> {
    return this.http.post<Trade>(`${this.base}/${id}/${action}`, null).pipe(catchError(toMessage));
  }
}

/** Converts RFC 7807 problem details from the API into one readable message. */
function toMessage(err: HttpErrorResponse) {
  if (err.status === 0) return throwError(() => new Error('Cannot reach the TradeDesk API on http://localhost:5152.'));
  const problem = err.error ?? {};
  const fieldErrors = problem.errors ? Object.values<string[]>(problem.errors).flat().join(' ') : '';
  return throwError(() => new Error(fieldErrors || problem.title || err.message));
}
