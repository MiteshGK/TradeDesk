import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { BlotterSummary, Trade, TradeAction, TradeStatus, previewInterest } from './trade.models';
import { TradeService } from './trade.service';

const today = () => new Date().toISOString().slice(0, 10);
const addDays = (iso: string, days: number) => {
  const d = new Date(`${iso}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

@Component({
  selector: 'app-root',
  imports: [ReactiveFormsModule, DecimalPipe, DatePipe],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit {
  private readonly api = inject(TradeService);
  private readonly fb = inject(FormBuilder);

  readonly statuses: TradeStatus[] = ['Booked', 'Settled', 'Matured', 'Cancelled'];

  readonly trades = signal<Trade[]>([]);
  readonly summary = signal<BlotterSummary | null>(null);
  readonly selectedId = signal<number | null>(null);
  readonly statusFilter = signal<TradeStatus | ''>('');
  readonly counterpartyFilter = signal('');
  readonly busy = signal(false);
  readonly message = signal<{ text: string; error: boolean } | null>(null);

  readonly selected = computed(() => this.trades().find(t => t.id === this.selectedId()) ?? null);

  readonly ticket = this.fb.nonNullable.group({
    counterparty: ['', [Validators.required, Validators.maxLength(100)]],
    side: ['Lend' as 'Lend' | 'Borrow'],
    principal: [100_000_000, [Validators.required, Validators.min(1)]],
    ratePercent: [6.5, [Validators.required, Validators.min(0.01), Validators.max(99.99)]],
    startDate: [today(), Validators.required],
    tenorDays: [1, [Validators.required, Validators.min(1), Validators.max(365)]],
  });

  private readonly ticketValue = toSignal(this.ticket.valueChanges, { initialValue: this.ticket.getRawValue() });

  readonly preview = computed(() => {
    const v = this.ticketValue();
    const principal = Number(v.principal) || 0;
    const tenor = Number(v.tenorDays) || 0;
    const interest = previewInterest(principal, Number(v.ratePercent) || 0, tenor);
    return {
      maturityDate: v.startDate && tenor > 0 ? addDays(v.startDate, tenor) : null,
      interest,
      maturityAmount: principal + interest,
    };
  });

  ngOnInit(): void {
    this.refresh();
  }

  refresh(keepSelection = this.selectedId()): void {
    this.busy.set(true);
    forkJoin({
      trades: this.api.list(this.statusFilter(), this.counterpartyFilter()),
      summary: this.api.summary(),
    }).subscribe({
      next: ({ trades, summary }) => {
        this.trades.set(trades);
        this.summary.set(summary);
        this.selectedId.set(trades.some(t => t.id === keepSelection) ? keepSelection : null);
        this.busy.set(false);
      },
      error: (e: Error) => this.fail(e),
    });
  }

  setStatusFilter(value: string): void {
    this.statusFilter.set(value as TradeStatus | '');
    this.refresh();
  }

  book(): void {
    if (this.ticket.invalid) {
      this.ticket.markAllAsTouched();
      return;
    }
    const v = this.ticket.getRawValue();
    const tradeDate = v.startDate < today() ? v.startDate : today();
    this.busy.set(true);
    this.api
      .book({
        counterparty: v.counterparty,
        side: v.side,
        principal: Number(v.principal),
        ratePercent: Number(v.ratePercent),
        tradeDate,
        startDate: v.startDate,
        maturityDate: addDays(v.startDate, Number(v.tenorDays)),
      })
      .subscribe({
        next: t => {
          this.ticket.controls.counterparty.reset('');
          this.ok(`Booked ${t.tradeRef}: ${t.side} ${t.principal.toLocaleString('en-IN')} with ${t.counterparty} at ${t.ratePercent}%`);
          this.refresh(t.id);
        },
        error: (e: Error) => this.fail(e),
      });
  }

  act(action: TradeAction): void {
    const trade = this.selected();
    if (!trade) return;
    this.busy.set(true);
    this.api.transition(trade.id, action).subscribe({
      next: t => {
        this.ok(`${t.tradeRef} is now ${t.status}`);
        this.refresh(t.id);
      },
      error: (e: Error) => this.fail(e),
    });
  }

  canAct(action: TradeAction): boolean {
    const s = this.selected()?.status;
    return !this.busy() && (action === 'mature' ? s === 'Settled' : s === 'Booked');
  }

  invalid(name: keyof typeof this.ticket.controls): boolean {
    const c = this.ticket.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  private ok(text: string): void {
    this.message.set({ text, error: false });
  }

  private fail(e: Error): void {
    this.busy.set(false);
    this.message.set({ text: e.message, error: true });
  }
}
