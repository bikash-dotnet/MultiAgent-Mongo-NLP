single-file solution: 
samples MongoDB, 
infers a flattened BSON-typed schema, 
prunes to relevant fields, 
compresses to a compact alias form, 
embeds MongoDB's official best-practice rules and real indexes, 
calls Gemini with optional caching, 
validates the resulting EJSON pipeline against the full schema (mirroring every prompt rule), and 
executes safely. 

The MCP server exposes it as four tools any AI client can call. 
Typical prompt: ~750–950 tokens — down from ~9,000 in naive setups — with higher accuracy and stricter safety.