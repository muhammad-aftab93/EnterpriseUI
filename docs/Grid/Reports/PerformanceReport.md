# 8. Performance report

All numbers were measured on the development machine with the code in this repository; none are estimates. Raw table: [benchmark-results.md](benchmark-results.md). Reproduce with `dotnet run -c Release --project benchmarks/EnterpriseUI.Benchmarks -- 1000000`. Regression guards with generous limits live in `tests/EnterpriseUI.PerformanceTests`.

## Summary (1,000,000 in-memory rows)

| Concern | Result | How it is achieved |
|---|---|---|
| Memory | 8 MB added by binding the grid; the 120 MB are the caller's own objects | Only a window (about 250 rows) is materialized; the source list is referenced, not copied |
| Bind | about 25 ms | Binding records the source and fetches the first window |
| Render | about 17 ms per 1400x800 frame (13 ms best) | Fixed row height, visible rows only, theme colors cached once per theme |
| Scroll | about 19 ms for a 2,400-row jump plus repaint; about 18 ms per single-row step including a full bitmap render | Scroll position maps to a row index arithmetically; the window is fetched around it |
| Filter | 16 ms (equality), 16 ms (contains) | Expression trees compiled once per query over the cached source |
| Sort | 449 ms numeric, 825 ms text | Query engine sort over the source; custom comparers sort in memory |
| Grouping | 294 ms by one column; 806 ms grouped and sorted | Groups are built once and flattened to a row list; collapsed groups cost one row each |
| Summaries | 311 ms for sum, average and count | Computed in the same query pass |
| Export | CSV 223 ms, JSON 305 ms, XLSX 1.7 s per 300,000 rows; PDF 274 ms per 20,000 rows | Streaming providers, no intermediate workbook |
| Print pagination | 53 ms for 50,000 rows | Pure geometry, no drawing during pagination |

## What the tests guard

- Binding a million rows must not materialize more than 2,000 rows, and must finish in under 5 seconds.
- A window deep in the list must be available within a second.
- Sort, filter and group over a million rows stay under 15 s, 8 s and 15 s (several times the measured values, so machine variance does not fail them but an O(n log n) to O(n^2) regression does).
- A 100,000-row grid paints a frame in under 150 ms and a jump-scroll step in under 250 ms.
- Streaming a 300,000-row CSV export grows memory by less than 120 MB.
- The grid must not retain copies of the bound rows.

## Honest notes

- Sorting text is about twice as slow as sorting numbers because of culture-aware comparison. Use a custom comparer with ordinal comparison where culture rules do not matter.
- Repeated identical queries are not faster than the first: the cache holds the source reference, not query results.
- XLSX is the slowest export because worksheet XML and the zip container are produced for every cell.
- GDI+ text rendering dominates frame time; very wide grids with many columns cost proportionally more per frame.
- Remote sources are bounded by their own latency; the grid cancels superseded requests but cannot make a slow service fast.
