import { Component, Input } from '@angular/core';

export type SkeletonVariant = 'text' | 'kpi-row' | 'card-grid' | 'table' | 'chart';

/** Shape-matched loading placeholders. A centered spinner tells the user "something is happening"
 * but nothing about what — and it collapses the layout, so the page visibly jumps when data
 * arrives. These mirror the geometry of the content that's coming (KPI row, table, chart, card
 * grid), so the page holds its shape and the wait reads as fast. Prefer this over `loading-state`
 * anywhere the incoming layout is known ahead of time. */
@Component({
  selector: 'app-skeleton',
  standalone: true,
  templateUrl: './skeleton.html',
  styleUrl: './skeleton.scss'
})
export class Skeleton {
  @Input() variant: SkeletonVariant = 'text';
  /** Rows (table), cards (card-grid), or lines (text) to render. */
  @Input() count = 5;
  /** Chart variant only — matched to the real chart's height so nothing shifts on load. */
  @Input() height = 260;

  get items(): number[] {
    return Array.from({ length: this.count }, (_, i) => i);
  }

  /** Staggered widths keep the placeholder from reading as a solid block of identical bars. */
  widthFor(index: number): string {
    const widths = ['92%', '78%', '85%', '70%', '88%'];
    return widths[index % widths.length];
  }
}
