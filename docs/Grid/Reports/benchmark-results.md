Benchmarks over 1,000,000 rows (rendering over the first 100,000, export capped as noted), 4+ logical cores, .NET 8, Release build. Produced by `dotnet run -c Release --project benchmarks/EnterpriseUI.Benchmarks -- 1000000`; numbers vary by machine, so re-run on your hardware before quoting them.

| Case | Min (ms) | Median (ms) | Max (ms) | Note |
|---|---:|---:|---:|---|
| memory: rows themselves (MB) | 120.2 | 120.2 | 120.2 | the bound objects, not owned by the grid |
| memory: added by binding the grid (MB) | 8.0 | 8.0 | 8.0 | 250 rows materialized |
| bind (RefreshDataAsync) | 20.8 | 25.2 | 131.6 |  |
| fetch window deep in the list | 0.0 | 0.0 | 0.1 |  |
| sort by Amount | 423.0 | 448.7 | 467.6 |  |
| sort by Name (text) | 743.9 | 825.3 | 838.9 |  |
| filter Region = EMEA | 15.3 | 16.1 | 31.6 |  |
| filter Name contains '99' | 13.6 | 15.7 | 29.8 |  |
| group by Region | 244.0 | 293.5 | 354.3 |  |
| group by Region + sort by Amount | 731.3 | 805.8 | 928.1 |  |
| column summaries (sum, average, count) | 254.8 | 311.2 | 336.5 |  |
| export 300,000 rows to csv | 152.2 | 223.3 | 232.5 |  |
| export 300,000 rows to json | 266.3 | 305.4 | 366.5 |  |
| export 300,000 rows to xlsx | 1,668.2 | 1,717.3 | 1,866.2 |  |
| export 20,000 rows to pdf | 236.2 | 274.1 | 348.9 | capped at 20,000 rows |
| print pagination (50,000 rows) | 46.4 | 52.8 | 92.4 |  |
| render one 1400x800 frame (100,000 rows) | 13.0 | 17.3 | 41.5 |  |
| scroll: jump 2,400 rows and render | 15.3 | 19.5 | 52.1 |  |
| scroll: 60 single-row steps and render each | 892.0 | 1,094.9 | 1,247.8 | about 18 ms per step including a full bitmap render |
