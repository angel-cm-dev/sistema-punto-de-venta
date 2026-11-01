import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react';

export interface SortState {
  sortBy: string;
  sortDir: 'asc' | 'desc';
}

/** Encabezado de tabla clicable que alterna asc → desc. */
export default function SortHeader({
  label,
  field,
  sort,
  onSort,
  className = '',
}: {
  label: string;
  field: string;
  sort: SortState;
  onSort: (s: SortState) => void;
  className?: string;
}) {
  const active = sort.sortBy === field;
  const Icon = !active ? ArrowUpDown : sort.sortDir === 'asc' ? ArrowUp : ArrowDown;
  return (
    <th className={className} aria-sort={active ? (sort.sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}>
      <button
        className="inline-flex items-center gap-1 uppercase"
        onClick={() => onSort({ sortBy: field, sortDir: active && sort.sortDir === 'asc' ? 'desc' : 'asc' })}
      >
        {label} <Icon className="h-3 w-3" />
      </button>
    </th>
  );
}
