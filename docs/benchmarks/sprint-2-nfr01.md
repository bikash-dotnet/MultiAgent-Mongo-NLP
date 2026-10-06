# Sprint 2 — BRD-NFR-01 Benchmarks

Measured in this environment (linux-x64 container, Release build). Budget per BRD-NFR-01
for in-memory steps; real ONNX embed recorded separately (split-budget decision).

| Step | Measured | Budget |
| --- | --- | --- |
| cache hit (full route incl. embed) | 21.02 ms | < 10 ms (lookup) / embed measured |
| real ONNX embed | 25.87 ms | measured ≈ 25 ms |
| slot extraction | 0.128 ms | < 5 ms |
| simple MQL render | 1.736 ms | < 2 ms |
