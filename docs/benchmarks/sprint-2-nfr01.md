# Sprint 2 — BRD-NFR-01 Benchmarks

Measured in this environment (linux-x64 container, Release build). Budget per BRD-NFR-01
for in-memory steps; real ONNX embed recorded separately (split-budget decision).

| Step | Measured | Budget |
| --- | --- | --- |
| cache hit (full route incl. embed) | 17.40 ms | < 10 ms (lookup) / embed measured |
| real ONNX embed | 3.92 ms | measured ≈ 25 ms |
| slot extraction | 0.005 ms | < 5 ms |
| simple MQL render | 0.218 ms | < 2 ms |
